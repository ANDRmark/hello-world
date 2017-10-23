using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;

namespace HelloWorld_asp_net_core
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            int x = 1;
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            loggerFactory.AddFile(Path.Combine(Directory.GetCurrentDirectory(), "logger.txt"));
            ILogger logger = loggerFactory.CreateLogger("Some Category Name");
            app.UseFileServer(enableDirectoryBrowsing: true);
            ///?admin=Vasya&token=8
            ////htmlpage.html?admin=Vasya&token=8
            app.UseErrorExplain();
            app.UseAuthentication();
            app.UseAdmin("Vasya");
            app.MapWhen(context =>
            {
                return new Random().Next(2) == 1;
            }, appin =>
           {
               appin.Use(async (context, next) =>
              {
                  await context.Response.WriteAsync("mapped Middleware 2");
                  await next();
              });
               appin.Run(async (context) =>
               {
                   await context.Response.WriteAsync("Inside Map Hello World!   x =  " + x++);
               });
           });

            app.Use(async  (context, next) =>
            {
                await next();
                await context.Response.WriteAsync("Middleware 1");
            });
            app.Run(async (context) =>
            {
                logger.LogWarning(" Request on  {0} adn x = " + x, context.Request.Path);
                await context.Response.WriteAsync("Hello World!   x =  " + x++);
            });
        }
    }
    public static class AdminExtension
    {
        public static IApplicationBuilder UseAdmin(this IApplicationBuilder builder, string adminname)
        {
            return builder.UseMiddleware<AdminMiddleware>(adminname);
        }
        public static IApplicationBuilder UseErrorExplain(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ErrorExplanationMiddleware>();
        }
        public static IApplicationBuilder UseAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AuthenticationMiddleware>();
        }
    }
    public class AdminMiddleware
    {
        RequestDelegate next;
        string pattern;
        public AdminMiddleware(RequestDelegate next, string adminname)
        {
            this.next = next;
            pattern = adminname;
        }
        public async Task Invoke(HttpContext context)
        {
            string name = context.Request.Query["admin"];
            if (name == null || name == "" || name != pattern)
                await next(context);
            else
            {
                await context.Response.WriteAsync("Hello " + name);
            }
        }
    }
    public class AuthenticationMiddleware
    {
        RequestDelegate next;
        public AuthenticationMiddleware(RequestDelegate d)
        {
            next = d;
        }
        public async Task Invoke(HttpContext context)
        {
            string token = context.Request.Query["token"];
            if (token != null && token != "")
            {
                await next(context);
            }
            else
            {
                context.Response.StatusCode = 403;
            }
        }
    }
    public class ErrorExplanationMiddleware
    {
        RequestDelegate next;
        public ErrorExplanationMiddleware(RequestDelegate d)
        {
            next = d;
        }
        public async Task Invoke(HttpContext context)
        {
            await next(context);
            if (context.Response.StatusCode == 403)
            {
                await context.Response.WriteAsync("You are not authorized");
            }
        }
    }
    public class MyFileLogger: ILogger
    {
        string filepath;
        object mylock = new object();
        public MyFileLogger(string file)
        {
            filepath = file;
        }
        public IDisposable BeginScope<TState>(TState s)
        {
            return null;
        }
        public bool IsEnabled(LogLevel level)
        {
            if (level != LogLevel.Trace && level != LogLevel.Debug && level != LogLevel.Information) return true;
            return false;
        }
        public void Log<Tstate> (LogLevel logLevel, EventId eventId, Tstate state, Exception exception, Func<Tstate, Exception, string> formatter)
        {
            if (formatter != null)
            {
                lock(mylock)
                {
                    File.AppendAllText(filepath, "its " + logLevel +"  " + formatter(state, exception) + Environment.NewLine);
                }
            }
        }
    }
    public class OwnFileLogerProvider : ILoggerProvider
    {
        string path;
        public OwnFileLogerProvider(string path)
        {
            this.path = path;
        }
        public ILogger CreateLogger(string categoryName)
        {
            return new MyFileLogger(path);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
    public static class FileLoggerExtension
    {
        public static ILoggerFactory AddFile(this ILoggerFactory factory, string filepath)
        {
            factory.AddProvider(new OwnFileLogerProvider(filepath));
            return factory;
        }
    }
}
