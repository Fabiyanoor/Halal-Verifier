
var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.Halal_Project_ApiService>("apiservice");

builder.AddProject<Projects.Halal_Project_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
