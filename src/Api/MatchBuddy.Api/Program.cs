using Business;
using MatchBuddy.Core;
using MatchBuddy.DataAccess;

namespace MatchBuddy.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();

            // Add Swagger for API documentation (optional, for development)
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Register custom services (Business, Core, DataAccess)
            builder.Services.AddBussinesRegistration();
            builder.Services.AddCoreRegistration();
            builder.Services.AddDataAccessRegistration();

            // Configure CORS to allow requests from localhost:3000 (frontend's port)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                    policy.AllowAnyOrigin()  // Allow any origin (can be restricted to specific origins like `http://localhost:3000` for production)
                          .AllowAnyMethod()    // Allow any HTTP method (GET, POST, etc.)
                          .AllowAnyHeader());  // Allow any headers
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                // Enable Swagger for development environment
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Enable HTTPS redirection (optional based on your environment)
            app.UseHttpsRedirection();

            // Apply CORS policy before authorization
            app.UseCors("AllowAll");

            // Enable Authorization middleware (if needed for authentication)
            app.UseAuthorization();

            // Map controllers for the API endpoints
            app.MapControllers();

            // Run the application
            app.Run();
        }
    }
}
