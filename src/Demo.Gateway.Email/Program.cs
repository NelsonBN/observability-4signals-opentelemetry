﻿using Common.Observability;
using Gateway.Email.Infrastructure.MessageBus;
using Gateway.Email.Infrastructure.Observability;
using Microsoft.AspNetCore.Hosting;

ContinuousProfiling.Setup();

var builder = Host.CreateDefaultBuilder(args);

builder
    .ConfigureWebHostDefaults(builder =>
        builder.Configure(app => app.AddObservability()))
    .ConfigureServices((hostContext, services) =>
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssemblyContaining<Program>());

        services.AddMessageBus();

        services.AddObservability();
    })
    .ConfigureLogging((hostContext, logging) =>
        logging.AddObservability());



var app = builder.Build();

await app.RunAsync();
