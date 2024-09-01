using AspNetCore.Proxy;
using MusicParty;
using MusicParty.Hub;
using MusicParty.MusicApi;
using MusicParty.MusicApi.Bilibili;
using MusicParty.MusicApi.NeteaseCloudMusic;
using MusicParty.MusicApi.QQMusic;

// 类似于 Spring Boot 的配置方式, sb 也有一个 sb appliction的class
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(); // 添加控制器支持
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

// 增加 swagger 支持
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

builder.Services.AddSignalR(); // gpt说这个是用于实现实时通信的

// Add music api
// 从配置文件中解析 api的配置, 添加到列表中;
var musicApiList = new List<IMusicApi>();
if (bool.Parse(builder.Configuration["MusicApi:NeteaseCloudMusic:Enabled"]))
{
    var api = new NeteaseCloudMusicApi(
        builder.Configuration["MusicApi:NeteaseCloudMusic:ApiServerUrl"],
        builder.Configuration["MusicApi:NeteaseCloudMusic:PhoneNo"],
        builder.Configuration["MusicApi:NeteaseCloudMusic:Cookie"]
    );
    api.Login();
    musicApiList.Add(api);
}

if (bool.Parse(builder.Configuration["MusicApi:QQMusic:Enabled"]))
{
    var api = new QQMusicApi(
        builder.Configuration["MusicApi:QQMusic:ApiServerUrl"],
        builder.Configuration["MusicApi:QQMusic:Cookie"]
    );
    musicApiList.Add(api);
}

if (bool.Parse(builder.Configuration["MusicApi:Bilibili:Enabled"]))
{
    var api = new BilibiliApi(
        builder.Configuration["MusicApi:Bilibili:SESSDATA"],
        builder.Configuration["MusicApi:Bilibili:PhoneNo"]
    );
    api.Login();
    musicApiList.Add(api);
}

// Add more music api provider in the future.
if (musicApiList.Count == 0)
    throw new Exception("Cannot start without any music api service.");

// 有很多服务都是用于增加单例的
builder.Services.AddSingleton<IEnumerable<IMusicApi>>(musicApiList); // 将服务作为单例添加到容器中
builder.Services.AddHttpContextAccessor(); // 增加 http context访问器
builder.Services.AddSingleton<UserManager>(); // 增加单例
builder.Services.AddAuthentication("Cookies").AddCookie("Cookies"); // 基于 cookie的身份验证
builder.Services.AddProxies();
builder.Services.AddSingleton<MusicBroadcaster>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UsePreprocess();

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
    endpoints.MapHub<MusicHub>("/music");
});

app.UseMusicProxy();

// Proxy the front end server.
// 运行前端
app.RunHttpProxy(builder.Configuration["FrontEndUrl"]);

app.Run();