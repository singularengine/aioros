var app = AiorosApplication.CreateBuilder(args).Build();
app.MapAioros();
return app.RunAioros();
