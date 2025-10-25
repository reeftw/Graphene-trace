namespace GrapheneTrace
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews(); 

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // CRITICAL FIX 1: This enables the server to find and serve static files 
            // (like your renamed clinician.html and GTLB-Data inside wwwroot).
            app.UseStaticFiles(); 

            app.UseRouting();
            
            app.UseAuthorization();

            // CRITICAL FIX 2: This standard method ensures the root URL (/) maps to Home/Index.
            // We remove the conflicting custom MapStaticAssets lines.
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
