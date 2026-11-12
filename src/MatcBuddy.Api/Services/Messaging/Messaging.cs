using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using MatcBuddy.Core.Messaging;
using MimeKit;

namespace MatcBuddy.Api.Services.Messaging
{
    /// <summary>Email:Smtp — Host boşsa e-postalar outbox klasörüne yazılır.</summary>
    public sealed class SmtpOptions
    {
        public const string SectionName = "Email:Smtp";
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? User { get; set; }
        public string? Password { get; set; }
        public string? From { get; set; }
        public string FromName { get; set; } = "MatcBuddy";
        /// <summary>true: 465 → SSL, diğer portlar → STARTTLS zorunlu. false: STARTTLS varsa kullanılır.</summary>
        public bool UseSsl { get; set; } = true;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
    }

    /// <summary>Sms:Netgsm — UserCode boşsa SMS'ler outbox klasörüne yazılır.</summary>
    public sealed class NetgsmOptions
    {
        public const string SectionName = "Sms:Netgsm";
        public string? UserCode { get; set; }
        public string? Password { get; set; }
        /// <summary>Netgsm'de onaylı gönderici başlığı (msgheader).</summary>
        public string? Header { get; set; }
        public string BaseUrl { get; set; } = "https://api.netgsm.com.tr";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(UserCode) && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(Header);
    }

    /// <summary>Messaging:OutboxPath (varsayılan App_Data/outbox, içerik köküne göre; .gitignore'da).</summary>
    public sealed class OutboxOptions
    {
        public string Path { get; set; } = string.Empty;
    }

    // ------------------------------------------------------------------ SMTP (MailKit)

    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _o;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(Microsoft.Extensions.Options.IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _o = options.Value;
            _logger = logger;
        }

        public bool IsConfigured => _o.IsConfigured;

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_o.FromName, _o.From));
            mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.To));
            mime.Subject = message.Subject;
            var body = new BodyBuilder { TextBody = message.TextBody };
            if (!string.IsNullOrEmpty(message.HtmlBody)) body.HtmlBody = message.HtmlBody;
            mime.Body = body.ToMessageBody();

            var security = _o.UseSsl
                ? (_o.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
                : SecureSocketOptions.StartTlsWhenAvailable;
            using var client = new SmtpClient { Timeout = 30000 };
            await client.ConnectAsync(_o.Host, _o.Port, security, ct);
            if (!string.IsNullOrEmpty(_o.User)) await client.AuthenticateAsync(_o.User, _o.Password ?? string.Empty, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            _logger.LogInformation("E-posta gönderildi: {To} ({Subject})", Mask.Email(message.To), message.Subject);
        }
    }

    // ------------------------------------------------------------------ Netgsm (REST v2)

    public sealed class NetgsmSmsSender : ISmsSender
    {
        public const string HttpClientName = "netgsm";
        private readonly NetgsmOptions _o;
        private readonly IHttpClientFactory _http;
        private readonly ILogger<NetgsmSmsSender> _logger;

        public NetgsmSmsSender(Microsoft.Extensions.Options.IOptions<NetgsmOptions> options, IHttpClientFactory http, ILogger<NetgsmSmsSender> logger)
        {
            _o = options.Value;
            _http = http;
            _logger = logger;
        }

        public bool IsConfigured => _o.IsConfigured;

        /// <summary>TR cep numarası → 5XXXXXXXXX (Netgsm'in beklediği biçim).</summary>
        public static string Normalize(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.Length >= 10 ? digits[^10..] : digits;
        }

        public async Task SendAsync(SmsMessage message, CancellationToken ct = default)
        {
            var client = _http.CreateClient(HttpClientName);
            using var req = new HttpRequestMessage(HttpMethod.Post, _o.BaseUrl.TrimEnd('/') + "/sms/rest/v2/send");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_o.UserCode}:{_o.Password}")));
            req.Content = JsonContent.Create(new
            {
                msgheader = _o.Header,
                encoding = "TR",
                iysfilter = "0", // bilgilendirme (OTP) — ticari ileti değil
                messages = new[] { new { msg = message.Text, no = Normalize(message.To) } },
            });
            using var res = await client.SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            string? code = null;
            try { code = JsonDocument.Parse(text).RootElement.TryGetProperty("code", out var c) ? c.GetString() : null; }
            catch (JsonException) { }
            if (!res.IsSuccessStatusCode || code != "00")
                throw new InvalidOperationException($"Netgsm gönderimi başarısız: HTTP {(int)res.StatusCode}, code {code ?? "?"}");
            _logger.LogInformation("SMS gönderildi: {To}", Mask.Phone(message.To));
        }
    }

    // ------------------------------------------------------------------ Outbox (geliştirme)

    /// <summary>Gönderici yapılandırılmamışsa her mesaj App_Data/outbox/*.txt dosyasına yazılır (testler kodu buradan okur).</summary>
    public sealed class OutboxWriter
    {
        private readonly string _root;
        private readonly ILogger<OutboxWriter> _logger;

        public OutboxWriter(IWebHostEnvironment env, IConfiguration configuration, ILogger<OutboxWriter> logger)
        {
            var path = configuration["Messaging:OutboxPath"];
            if (string.IsNullOrWhiteSpace(path)) path = System.IO.Path.Combine("App_Data", "outbox");
            _root = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(env.ContentRootPath, path);
            _logger = logger;
        }

        public string Root => _root;

        public async Task WriteAsync(string kind, string to, string content, CancellationToken ct)
        {
            Directory.CreateDirectory(_root);
            var file = System.IO.Path.Combine(_root, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{kind}-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(file, content, new UTF8Encoding(false), ct);
            _logger.LogWarning("Outbox: {Kind} → {To} gönderilmedi, dosyaya yazıldı: {File}", kind,
                kind == "email" ? Mask.Email(to) : Mask.Phone(to), System.IO.Path.GetFileName(file));
        }
    }

    public sealed class OutboxEmailSender : IEmailSender
    {
        private readonly OutboxWriter _writer;
        public OutboxEmailSender(OutboxWriter writer) => _writer = writer;
        public bool IsConfigured => true;

        public Task SendAsync(EmailMessage m, CancellationToken ct = default) =>
            _writer.WriteAsync("email", m.To,
                $"To: {m.To}\nToName: {m.ToName}\nSubject: {m.Subject}\n\n{m.TextBody}\n\n----- HTML -----\n{m.HtmlBody}\n", ct);
    }

    public sealed class OutboxSmsSender : ISmsSender
    {
        private readonly OutboxWriter _writer;
        public OutboxSmsSender(OutboxWriter writer) => _writer = writer;
        public bool IsConfigured => true;

        public Task SendAsync(SmsMessage m, CancellationToken ct = default) =>
            _writer.WriteAsync("sms", m.To, $"To: {m.To}\n\n{m.Text}\n", ct);
    }

    // ------------------------------------------------------------------ Kuyruk + arka plan gönderimi

    public sealed class OutgoingMessageQueue : IOutgoingMessageQueue
    {
        private readonly Channel<object> _channel = Channel.CreateBounded<object>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

        public ChannelReader<object> Reader => _channel.Reader;
        public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);
        public void Enqueue(SmsMessage message) => _channel.Writer.TryWrite(message);
    }

    public sealed class OutgoingMessageService : BackgroundService
    {
        private readonly OutgoingMessageQueue _queue;
        private readonly IEmailSender _email;
        private readonly ISmsSender _sms;
        private readonly ILogger<OutgoingMessageService> _logger;

        public OutgoingMessageService(OutgoingMessageQueue queue, IEmailSender email, ISmsSender sms, ILogger<OutgoingMessageService> logger)
        {
            _queue = queue;
            _email = email;
            _sms = sms;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                for (var attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        if (item is EmailMessage e) await _email.SendAsync(e, stoppingToken);
                        else if (item is SmsMessage s) await _sms.SendAsync(s, stoppingToken);
                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Mesaj gönderilemedi (deneme {Attempt}/2): {Kind}", attempt, item.GetType().Name);
                        if (attempt == 1) await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
            }
        }
    }
}
