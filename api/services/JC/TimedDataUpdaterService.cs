﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SS.Api.helpers;

namespace SS.Api.services.jc
{
    internal class TimedDataUpdaterService : IHostedService, IDisposable
    {
        private ILogger Logger { get; }
        private Timer _timer;
        public IServiceProvider Services { get; }
        private readonly TimeSpan _jcSynchronizationPeriod;

        public TimedDataUpdaterService(IServiceProvider services, ILogger<TimedDataUpdaterService> logger, IConfiguration configuration)
        {
            Services = services;
            Logger = logger;
            _jcSynchronizationPeriod = TimeSpan.Parse(configuration.GetNonEmptyValue("JCSynchronization:Period"));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"Timed Background Service is starting with a period of {_jcSynchronizationPeriod}.");
            _timer = new Timer(DoWork, null, new TimeSpan(), _jcSynchronizationPeriod);
            return Task.CompletedTask;
        }

        private async void DoWork(object state)
        {
            try
            {
                Logger.LogInformation("Timed Background Service is working.");

                using var scope = Services.CreateScope();
                var justinDataUpdaterService =
                    scope.ServiceProvider
                        .GetRequiredService<JCDataUpdaterService>();

                Logger.LogInformation("Syncing Regions.");
                await justinDataUpdaterService.SyncRegions();
                Logger.LogInformation("Syncing Locations.");
                await justinDataUpdaterService.SyncLocations();
                Logger.LogInformation("Syncing CourtRooms.");
                await justinDataUpdaterService.SyncCourtRooms();
                Logger.LogInformation("Finished Syncing.");
            }
            catch (Exception e)
            {
                Logger.LogError(e, "An error happened while syncing regions/locations/courtrooms.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Timed Background Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
