using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QBMS.Application.Interfaces;
using QBMS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using QBMS1.Services;
using QBMS.Infrastructure.Repositories;
using QBMS.Application.Models;

namespace QBMS1
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews(options =>
            {
                // activity log of every action done by a logged-in user
                options.Filters.Add<ActivityLogActionFilter>();
            });

            // Login password is encrypted in the browser (RSA) and decrypted here.
            services.AddMemoryCache();
            services.AddSingleton<ILoginPasswordCrypto, LoginPasswordCrypto>();

            var inoesConnectionString =
                Configuration.GetConnectionString("INOES");

            var qbmsConnectionString =
                Configuration.GetConnectionString("QBMS");

            services.AddSingleton<IActivityLogRepository>(
                new ActivityLogRepository(qbmsConnectionString));
            services.AddScoped<ActivityLogActionFilter>();
            //TOS SYNC
            services.AddSingleton<ITosSyncRepository>(
                new TosSyncRepository(
                    inoesConnectionString,
                    qbmsConnectionString));
            //TOS REPO
            services.AddSingleton<ITosRepository>(
                new TosRepository(
                    qbmsConnectionString));
            //TOS Hirerachy
            services.AddSingleton<ITosHierarchyRepository>(
                new TosHierarchyRepository(
                    qbmsConnectionString));
            services.AddScoped<ITosHierarchyService, TosHierarchyService>();
            //settercreation
            services.AddSingleton<ISetterQuestionCreationRepository>(new SetterQuestionCreationRepository(qbmsConnectionString));
            services.AddScoped<ISetterQuestionCreationService, SetterQuestionCreationService>();


            
            services.AddSingleton<IVetterQuestionReviewRepository>(new VetterQuestionReviewRepository(qbmsConnectionString));
            services.AddScoped<
                IVetterQuestionReviewService,
                VetterQuestionReviewService>();

            //TOS Service
            services.AddScoped<ITosService, TosService>();
            //User Repo
            services.AddSingleton<IUserRepository>(new UserRepository(qbmsConnectionString));
            //Question Assignment
            
            services.AddSingleton<IQuestionAssignmentRepository>(new QuestionAssignmentRepository(qbmsConnectionString));
            services.AddScoped<IQuestionAssignmentService,QuestionAssignmentService>();
            services.AddSingleton<ISetterRepository>(
    new SetterRepository(
        qbmsConnectionString));

            services.AddScoped<
                ISetterService,
                SetterService>();


            services.AddSingleton<IQuestionQuotaRepository>(
    new QuestionQuotaRepository(
        qbmsConnectionString));

            services.AddScoped<
                IQuestionQuotaService,
                QuestionQuotaService>();

            services.AddSingleton<
    IQuestionWorkflowRepository>(
    new QuestionWorkflowRepository(
        qbmsConnectionString));

            services.AddScoped<
                IQuestionWorkflowService,
                QuestionWorkflowService>();


            services.AddSingleton<IVetterRepository>(
    new VetterRepository(
        qbmsConnectionString));

            services.AddScoped<
                IVetterService,
                VetterService>();


            services.AddSingleton<IReportRepository>(new ReportRepository(qbmsConnectionString));

            services.AddSingleton<IAdminQuestionRepository>(
    new AdminQuestionRepository(
        qbmsConnectionString,inoesConnectionString));

            services.AddScoped<
                IAdminQuestionService,
                AdminQuestionService>();


            services.AddSingleton<
    IAuthenticationRepository>(
    new AuthenticationRepository(
        qbmsConnectionString));


            services.AddScoped<
                QBMS.Application.Interfaces.IAuthenticationService,
                QBMS.Infrastructure.Services.AuthenticationService>();

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options=> {
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.ExpireTimeSpan = System.TimeSpan.FromMinutes(60);
                options.SlidingExpiration = true;
            });

            // Settings: default password + HRCDF API
            var securitySettings =
                Configuration.GetSection("Security").Get<SecuritySettings>()
                ?? new SecuritySettings();
            var hrcdfSettings =
                Configuration.GetSection("Hrcdf").Get<HrcdfSettings>()
                ?? new HrcdfSettings();

            services.AddSingleton(securitySettings);
            services.AddSingleton(hrcdfSettings);

            // Duplicate check: offline Python API + background worker for uploaded Word / Excel files
            var duplicateCheckSettings =
                Configuration.GetSection("DuplicateCheck").Get<DuplicateCheckSettings>()
                ?? new DuplicateCheckSettings();

            services.AddSingleton(duplicateCheckSettings);

            services.AddHttpClient<IDuplicateCheckService, DuplicateCheckService>(client =>
            {
                client.Timeout = System.TimeSpan.FromSeconds(
                    duplicateCheckSettings.TimeoutSeconds > 0
                        ? duplicateCheckSettings.TimeoutSeconds
                        : 60);
            });

            services.AddSingleton<IUploadBatchRepository>(
                new UploadBatchRepository(qbmsConnectionString));

            services.AddHostedService<DuplicateCheckBackgroundService>();

            services.AddHttpClient<IHrcdfService, HrcdfService>(client =>
            {
                client.Timeout = System.TimeSpan.FromSeconds(
                    hrcdfSettings.TimeoutSeconds > 0
                        ? hrcdfSettings.TimeoutSeconds
                        : 15);
            });

            services.AddSingleton<
    IUserManagementRepository>(
    new UserManagementRepository(
        qbmsConnectionString));

            services.AddScoped<
                IUserManagementService,
                UserManagementService>();

            services.AddHttpContextAccessor();

            services.AddScoped<
                ICurrentUserService,
                CurrentUserService>();

    //        services.AddScoped<
    //IQuestionUploadService,
    //QuestionUploadService>();


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<ForcePasswordChangeMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Account}/{action=Login}/{id?}");
            });
        }
    }
}
