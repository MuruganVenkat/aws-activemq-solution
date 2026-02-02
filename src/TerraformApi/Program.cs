using Amazon.SecretsManager;
using TerraformApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Load environments.json
builder.Configuration.AddJsonFile("environments.json", optional: false, reloadOnChange: true);

// Register AWS Secrets Manager (uses default credential chain)
builder.Services.AddAWSService<IAmazonSecretsManager>();

// Register TerraformService
builder.Services.AddScoped<TerraformService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
