using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MatcBuddy.Core.Messaging;

namespace MatcBuddy.Business.Email
{
    /// <summary>Bağlantılar (API katmanı doldurur): site adresi ve imzalı abonelikten çıkma linki</summary>
    public interface IEmailLinks
    {
        string FrontendBaseUrl { get; }
        string UnsubscribeUrl(int playerId);
    }

    /// <summary>
    /// Şablon metnini doldurur ({{ad}}, {{mac}}…) ve uygulamanın görünümüne uygun (koyu lacivert + neon yeşil),
    /// e-posta istemcileriyle uyumlu (tablo tabanlı, satır içi stil) HTML'e yerleştirir.
    /// Gövde düz metindir, HTML yazılamaz (her şey kodlanır). Basit biçimler:
    /// <c># Başlık</c> (ilk satırdaysa üst bantta büyük başlık), <c>## Ara başlık</c>, <c>- madde</c>,
    /// <c>&gt; vurgu kutusu</c>, <c>| Etiket: değer</c> (bilgi kartı satırı), <c>**kalın**</c>, <c>---</c> (ayraç).
    /// Boş satır paragrafı ayırır.
    /// </summary>
    public static class EmailLayout
    {
        // Uygulama renkleri (MatcBuddyReact: --mb-bg / --mb-surface / --mb-primary-fill / --mb-primary-ink)
        private const string Night = "#070b12";
        private const string NightSoft = "#0e1520";
        private const string Lime = "#c6f432";
        private const string LimeInk = "#0b1405";
        private const string Page = "#eef2ea";
        private const string Ink = "#111827";
        private const string Muted = "#6b7280";
        private const string Line = "#e2e8dc";
        private const string Font = "'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

        private static readonly Regex Field = new(@"\{\{\s*([a-zA-ZçğıöşüÇĞİÖŞÜ_]+)\s*\}\}", RegexOptions.Compiled);
        private static readonly Regex Bold = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

        /// <summary>Kullanılabilen alanlar (yönetim ekranında gösterilir)</summary>
        public static readonly string[] Fields = { "ad", "soyad", "kullanici", "site", "tesis", "mac", "tarih", "saat", "saha", "adres", "kalan", "link" };

        public static string Fill(string? text, IReadOnlyDictionary<string, string?> values) =>
            Field.Replace(text ?? string.Empty, m =>
                values.TryGetValue(m.Groups[1].Value.ToLowerInvariant(), out var v) ? v ?? string.Empty : string.Empty);

        public static Dictionary<string, string?> PersonValues(string? name, string? surname, string? userName, string site) => new()
        {
            ["ad"] = string.IsNullOrWhiteSpace(name) ? userName : name,
            ["soyad"] = surname,
            ["kullanici"] = userName,
            ["site"] = site,
        };

        private static string E(string s) => WebUtility.HtmlEncode(s);

        /// <summary>Satır içi: HTML kodlanır, **kalın** işlenir</summary>
        private static string Inline(string s) =>
            Bold.Replace(E(s), m => $@"<strong style=""font-weight:700;color:{Ink}"">{m.Groups[1].Value}</strong>");

        private enum Kind { Blank, Para, Heading, Sub, Bullet, Quote, Info, Rule }

        private static (Kind kind, string text) Classify(string raw)
        {
            var l = raw.Trim();
            if (l.Length == 0) return (Kind.Blank, "");
            if (l == "---") return (Kind.Rule, "");
            if (l.StartsWith("## ")) return (Kind.Sub, l[3..].Trim());
            if (l.StartsWith("# ")) return (Kind.Heading, l[2..].Trim());
            if (l.StartsWith("- ") || l.StartsWith("• ")) return (Kind.Bullet, l[2..].Trim());
            if (l.StartsWith(">")) return (Kind.Quote, l[1..].Trim());
            if (l.StartsWith("|")) return (Kind.Info, l[1..].Trim());
            return (Kind.Para, l);
        }

        /// <summary>Gövdeyi bloklara ayırır; baştaki "# Başlık" üst banda alınır</summary>
        private static (string? hero, string html) RenderBody(string body)
        {
            var lines = body.Replace("\r\n", "\n").Trim().Split('\n').Select(Classify).ToList();
            string? hero = null;
            if (lines.Count > 0 && lines[0].kind == Kind.Heading)
            {
                hero = lines[0].text;
                lines.RemoveAt(0);
            }

            var sb = new StringBuilder();
            var i = 0;
            while (i < lines.Count)
            {
                var (kind, text) = lines[i];
                // Ardışık aynı türdeki satırlar tek blok
                var group = new List<string>();
                while (i < lines.Count && lines[i].kind == kind && kind is not (Kind.Heading or Kind.Sub or Kind.Rule))
                {
                    group.Add(lines[i].text);
                    i++;
                }
                if (group.Count == 0)
                {
                    group.Add(text);
                    i++;
                }

                switch (kind)
                {
                    case Kind.Blank:
                        break;
                    case Kind.Para:
                        sb.Append($@"<p style=""margin:0 0 16px"">{string.Join("<br>", group.Select(Inline))}</p>");
                        break;
                    case Kind.Heading:
                        sb.Append($@"<h2 style=""margin:8px 0 12px;font-size:20px;line-height:1.3;color:{Ink};font-weight:800"">{Inline(text)}</h2>");
                        break;
                    case Kind.Sub:
                        sb.Append($@"<h3 style=""margin:6px 0 8px;font-size:13px;letter-spacing:.08em;text-transform:uppercase;color:#467211;font-weight:800"">{Inline(text)}</h3>");
                        break;
                    case Kind.Rule:
                        sb.Append($@"<div style=""height:1px;background:{Line};margin:20px 0;line-height:1px;font-size:1px"">&nbsp;</div>");
                        break;
                    case Kind.Bullet:
                        sb.Append(@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" style=""margin:0 0 16px;width:100%"">");
                        foreach (var g in group)
                            sb.Append($@"<tr><td valign=""top"" style=""width:22px;padding:4px 0""><span style=""display:inline-block;width:8px;height:8px;border-radius:4px;background:{Lime};border:1px solid #9ccc1a;margin-top:6px""></span></td><td style=""padding:2px 0"">{Inline(g)}</td></tr>");
                        sb.Append("</table>");
                        break;
                    case Kind.Quote:
                        sb.Append($@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" style=""margin:0 0 18px;width:100%""><tr><td style=""background:#f5fde0;border-left:4px solid {Lime};border-radius:10px;padding:14px 16px;color:{LimeInk};font-weight:600"">{string.Join("<br>", group.Select(Inline))}</td></tr></table>");
                        break;
                    case Kind.Info:
                        sb.Append($@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" style=""margin:0 0 18px;width:100%;border:1px solid {Line};border-radius:12px;border-collapse:separate;overflow:hidden"">");
                        for (var k = 0; k < group.Count; k++)
                        {
                            var g = group[k];
                            var cut = g.IndexOf(':');
                            var label = cut > 0 ? g[..cut].Trim() : "";
                            var value = cut > 0 ? g[(cut + 1)..].Trim() : g;
                            var border = k == group.Count - 1 ? "" : $"border-bottom:1px solid {Line};";
                            sb.Append($@"<tr><td style=""{border}padding:11px 14px;background:#f8faf5;color:{Muted};font-size:13px;width:34%;vertical-align:top"">{Inline(label)}</td><td style=""{border}padding:11px 14px;font-weight:600;color:{Ink}"">{Inline(value)}</td></tr>");
                        }
                        sb.Append("</table>");
                        break;
                }
            }
            return (hero, sb.ToString());
        }

        /// <summary>Düz metin sürümü: biçim işaretleri temizlenir</summary>
        private static string PlainText(string body) =>
            string.Join("\n", body.Replace("\r\n", "\n").Trim().Split('\n').Select(raw =>
            {
                var (kind, text) = Classify(raw);
                text = Bold.Replace(text, "$1");
                return kind switch
                {
                    Kind.Heading or Kind.Sub => text.ToUpper(CultureInfo.CurrentCulture),
                    Kind.Bullet => "• " + text,
                    Kind.Rule => "────────",
                    Kind.Blank => "",
                    _ => text,
                };
            }));

        // Kimlik e-postalarında site adresi verilmez; logo yine canlı siteden yüklenir
        private const string DefaultSite = "https://matcbuddy.com";

        /// <summary>Uygulama logosu (sitedeki logo192.png) + MatcBuddy yazısı; resim engellenirse alt metin görünür</summary>
        private static string Logo(string? site)
        {
            var src = $"{(site ?? DefaultSite).TrimEnd('/')}/logo192.png";
            var mark = $@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0""><tr><td style=""width:40px;height:40px;vertical-align:middle""><img src=""{E(src)}"" width=""40"" height=""40"" alt=""MatcBuddy"" style=""display:block;width:40px;height:40px;border:0;border-radius:10px;font-family:{Font};font-size:12px;font-weight:800;color:{Lime}""></td><td style=""padding-left:10px;font-family:{Font};font-size:18px;font-weight:900;letter-spacing:.06em;color:#ffffff"">MATC<span style=""color:{Lime}"">BUDDY</span></td></tr></table>";
            return site == null ? mark : $@"<a href=""{E(site)}"" style=""text-decoration:none"">{mark}</a>";
        }

        /// <summary>
        /// Ortak kabuk: koyu üst bant (logo + isteğe bağlı büyük başlık), beyaz içerik kartı, alt bilgi.
        /// Kimlik e-postaları (AuthTemplates) da bunu kullanır.
        /// </summary>
        public static string Shell(string subject, string? preheader, string? heroTitle, string contentHtml, string footerHtml, string? site = null)
        {
            var hero = string.IsNullOrWhiteSpace(heroTitle)
                ? ""
                : $@"<tr><td style=""padding:6px 28px 30px;font-family:{Font};font-size:26px;line-height:1.25;font-weight:800;color:#ffffff"">{Inline(heroTitle!).Replace($"color:{Ink}", $"color:{Lime}")}</td></tr>";
            var pre = string.IsNullOrWhiteSpace(preheader)
                ? ""
                : $@"<div style=""display:none;max-height:0;overflow:hidden;opacity:0;color:transparent"">{E(preheader!)}&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;</div>";
            return $@"<!doctype html>
<html lang=""{E(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)}""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><meta name=""color-scheme"" content=""light""><meta name=""supported-color-schemes"" content=""light""><title>{E(subject)}</title></head>
<body style=""margin:0;padding:0;background:{Page};font-family:{Font};color:{Ink};-webkit-text-size-adjust:100%"">
{pre}
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""background:{Page}""><tr><td align=""center"" style=""padding:24px 12px"">
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width:580px"">
<tr><td style=""background:{Night};background-image:linear-gradient(135deg,{Night} 0%,{NightSoft} 60%,#1a2a0a 100%);border-radius:18px 18px 0 0"">
<table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
<tr><td style=""padding:24px 28px 18px"">{Logo(site)}</td></tr>
{hero}
<tr><td style=""height:4px;background:{Lime};line-height:4px;font-size:4px"">&nbsp;</td></tr>
</table></td></tr>
<tr><td style=""background:#ffffff;padding:28px;font-family:{Font};font-size:15px;line-height:1.65;color:#1f2937;border-radius:0 0 18px 18px"">{contentHtml}</td></tr>
<tr><td style=""padding:20px 12px 8px;text-align:center;font-family:{Font};font-size:12px;line-height:1.6;color:{Muted}"">{footerHtml}</td></tr>
</table></td></tr></table></body></html>";
        }

        /// <summary>Uygulama düğmesi (neon yeşil, koyu yazı)</summary>
        public static string Button(string text, string url) =>
            $@"<table role=""presentation"" cellspacing=""0"" cellpadding=""0"" align=""center"" style=""margin:10px auto 6px""><tr><td style=""background:{Lime};border-radius:999px""><a href=""{E(url)}"" style=""display:inline-block;padding:14px 30px;font-family:{Font};font-size:16px;font-weight:800;color:{LimeInk};text-decoration:none;border-radius:999px"">{E(text)} &rarr;</a></td></tr></table>";

        public static EmailMessage Build(string to, string? toName, string subject, string body, string? buttonText, string? buttonUrl,
            string? unsubscribeUrl, string? site = null, string? senderNote = null)
        {
            var (hero, content) = RenderBody(body);
            var hasButton = !string.IsNullOrWhiteSpace(buttonText) && IsHttp(buttonUrl);
            if (hasButton) content += Button(buttonText!, buttonUrl!);

            var link = $"color:{Muted};text-decoration:underline";
            var footer = new StringBuilder();
            if (IsHttp(site))
            {
                var s = site!.TrimEnd('/');
                footer.Append($@"<p style=""margin:0 0 10px""><a href=""{E(s)}/home"" style=""{link}"">Maçlar</a> &nbsp;·&nbsp; <a href=""{E(s)}/venues"" style=""{link}"">Sahalar</a> &nbsp;·&nbsp; <a href=""{E(s)}/settings"" style=""{link}"">Bildirim ayarları</a></p>");
            }
            if (!string.IsNullOrWhiteSpace(senderNote)) footer.Append($@"<p style=""margin:0 0 6px"">{E(senderNote!)}</p>");
            footer.Append(@"<p style=""margin:0 0 6px"">MatcBuddy · Halı saha maçını kur, kadroyu tamamla.</p>");
            if (unsubscribeUrl != null)
                footer.Append($@"<p style=""margin:0""><a href=""{E(unsubscribeUrl)}"" style=""{link}"">Bu tür e-postaları almak istemiyorum</a></p>");

            var plainBody = PlainText(body);
            var preheader = plainBody.Split('\n').Select(l => l.Trim())
                .FirstOrDefault(l => l.Length > 0 && !string.Equals(l, hero, StringComparison.CurrentCultureIgnoreCase)
                                     && l != hero?.ToUpper(CultureInfo.CurrentCulture));
            var html = Shell(subject, preheader, hero, content, footer.ToString(), IsHttp(site) ? site!.TrimEnd('/') : null);
            var text = plainBody
                + (hasButton ? $"\n\n{buttonText}: {buttonUrl}" : "")
                + (string.IsNullOrWhiteSpace(senderNote) ? "" : $"\n\n{senderNote}")
                + (unsubscribeUrl == null ? "" : $"\n\nAbonelikten çık: {unsubscribeUrl}");
            return new EmailMessage(to, toName, subject, text, html);
        }

        public static bool IsHttp(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

        /// <summary>"3 saat", "1 gün" gibi</summary>
        public static string Remaining(int minutes) =>
            minutes % 1440 == 0 ? $"{minutes / 1440} gün" : minutes % 60 == 0 ? $"{minutes / 60} saat" : $"{minutes} dakika";
    }
}
