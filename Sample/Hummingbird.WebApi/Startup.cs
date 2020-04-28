﻿using Hummingbird.Extensions.HealthChecks;
using Hummingbird.Extensions.EventBus.Abstractions;
using Hummingbird.Extensions.EventBus.RabbitMQ;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hummingbird.WebApi
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
            services.AddMvc();
            services.AddHealthChecks(checks =>
            {
                checks.WithDefaultCacheDuration(TimeSpan.FromSeconds(5));
                checks.AddMySqlCheck("mysql", "Server=dev.mysql.service.consul;Port=63307;Database=lms_openapi_cn_dev; User=lms-dev;Password=97bL8AtWmlfxQtK10Afg;pooling=True;minpoolsize=1;maxpoolsize=100;connectiontimeout=180;SslMode=None");
                checks.AddSqlCheck("123", "Data Source=test.sqlserver.service.consul,63341;Initial Catalog=ZT_ConfigCenter_TEST;User Id=tms-test;Password=qtvf12Croexy4cXH7lZB");
                checks.AddRedisCheck("redis", Configuration["redis:0:connectionString"]);
                checks.AddRabbitMQCheck("rabbitmq", factory =>
                {
                    factory.WithEndPoint(Configuration["EventBus:HostName"] ?? "localhost", int.Parse(Configuration["EventBus:Port"] ?? "5672"));
                    factory.WithAuth(Configuration["EventBus:UserName"] ?? "guest", Configuration["EventBus:Password"] ?? "guest");
                    factory.WithExchange(Configuration["EventBus:VirtualHost"] ?? "/");
                });
            });
            
            services.AddHummingbird(hummingbird =>
            {
                hummingbird                
                 .AddResilientHttpClient((orign, option) =>
                 {
                     if (string.IsNullOrEmpty(orign))
                     {
                         option.DurationSecondsOfBreak = int.Parse(Configuration["HttpClient:DurationSecondsOfBreak"]);
                         option.ExceptionsAllowedBeforeBreaking = int.Parse(Configuration["HttpClient:ExceptionsAllowedBeforeBreaking"]);
                         option.RetryCount = int.Parse(Configuration["HttpClient:RetryCount"]);
                         option.TimeoutMillseconds = int.Parse(Configuration["HttpClient:TimeoutMillseconds"]);
                     }
                     else
                     {
                         option.DurationSecondsOfBreak = int.Parse(Configuration[$"HttpClient:DurationSecondsOfBreak"]);
                         option.ExceptionsAllowedBeforeBreaking = int.Parse(Configuration["HttpClient:ExceptionsAllowedBeforeBreaking"]);
                         option.RetryCount = int.Parse(Configuration["HttpClient:RetryCount"]);
                         option.TimeoutMillseconds = int.Parse(Configuration["HttpClient:TimeoutMillseconds"]);
                     }
                 })
                .AddCache(option =>
                {
                    option.ConfigName = "HummingbirdCache";
                    option.CacheRegion = Configuration["SERVICE_NAME"];
                })
                .AddCacheing(option =>
                {

                    option.WithDb(0);
                    option.WithKeyPrefix("");
                    option.WithPassword("123456");
                    option.WithReadServerList("192.168.109.44:6379");
                    option.WithWriteServerList("192.168.109.44:6379");
                    option.WithSsl(false);
                })
                .AddIdempotency(option =>
                {
                    option.Druation = TimeSpan.FromMinutes(5);
                    option.CacheRegion = "Idempotency";
                })

                .AddSnowflakeUniqueIdGenerator(IdGenerator =>
                {
                    IdGenerator.CenterId = 0;
                    IdGenerator.UseStaticWorkIdCreateStrategy(0);
                })
                .AddOpenTracing(builder => {

                    builder.AddJaeger(Configuration.GetSection("Tracing"));
                })
                .AddEventBus((builder) =>
                {
                    var Database_Server = Configuration["Database:SQLServer:Server"];
                    var Database_Database = Configuration["Database:SQLServer:Database"];
                    var Database_UserId = Configuration["Database:SQLServer:UserId"];
                    var Database_Password = Configuration["Database:SQLServer:Password"];
                    var DatabaseConnectionString = $"Server={Database_Server};Database={Database_Database};User Id={Database_UserId};Password={Database_Password};MultipleActiveResultSets=true";

                    builder
                    .AddRabbitmq(factory =>
                    {
                        factory.WithEndPoint(Configuration["EventBus:HostName"] ?? "localhost", int.Parse(Configuration["EventBus:Port"] ?? "5672"));
                        factory.WithAuth(Configuration["EventBus:UserName"] ?? "guest", Configuration["EventBus:Password"] ?? "guest");
                        factory.WithExchange(Configuration["EventBus:VirtualHost"] ?? "/");
                        factory.WithReceiver(PreFetch: 10, ReceiverMaxConnections: 1, ReveiverMaxDegreeOfParallelism: 1);
                        factory.WithSender(10);
                    });
                    //builder.AddKafka(option =>
                    //{
                    //    option.WithSenderConfig(new Confluent.Kafka.ProducerConfig()
                    //    {

                    //        EnableDeliveryReports = true,
                    //        BootstrapServers = "192.168.78.29:9092,192.168.78.30:9092,192.168.78.31:9092",
                    //        // Debug = "msg" //  Debug = "broker,topic,msg"
                    //    });

                    //    option.WithReceiverConfig(new Confluent.Kafka.ConsumerConfig()
                    //    {
                    //        // Debug= "consumer,cgrp,topic,fetch",
                    //        GroupId = "test-consumer-group",
                    //        BootstrapServers = "192.168.78.29:9092,192.168.78.30:9092,192.168.78.31:9092",
                    //    });
                    //    option.WithReceiver(1, 1);
                    //    option.WithSender(10, 3, 1000 * 5, 50);
                    //});
                    //.AddSqlServerEventLogging(a =>
                    // {
                    //     a.WithEndpoint(DatabaseConnectionString);
                    // });
                })
                .AddConsulDynamicRoute(Configuration, s =>
                 {
                     s.AddTags("22");
                 });

            });


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
            var logger = app.ApplicationServices.GetRequiredService<ILogger<IEventLogger>>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHummingbird(humming =>
            {
              
                humming.UseEventBus(sp =>
                {
                    sp.UseSubscriber(eventbus =>
                    {
                        //eventbus.RegisterBatch<Events.NewMsgEvent, Events.NewMsgEventBatchHandler>("NewMsgEventBatchHandler", "NewMsgEvent");
                        eventbus.Register<NewMsgEvent, NewMsgEventHandler>("NewMsgEventBatchHandler", "TestTopic");
                        //eventbus.RegisterBatch<ChangeDataCaptureEvent, ChangeDataCaptureEventToESIndexHandler>("", "#");



                        //订阅消息
                        eventbus.Subscribe((Messages) =>
                        {
                            foreach (var message in Messages)
                            {
                                logger.LogDebug($"ACK: queue {message.QueueName} route={message.RouteKey} messageId:{message.MessageId}");
                            }

                        }, async (obj) =>
                        {
                            throw new Exception();
                            foreach (var message in obj.Messages)
                            {
                                logger.LogError($"NAck: queue {message.QueueName} route={message.RouteKey} messageId:{message.MessageId}");
                            }

                            //消息消费失败执行以下代码
                            if (obj.Exception != null)
                            {
                                logger.LogError(obj.Exception, obj.Exception.Message);
                            }

                            var events = obj.Messages.Select(message => message.WaitAndRetry(a => 5,3)).ToList();

                            var ret = !(await eventBus.PublishAsync(events));

                            return ret;
                        });
                    });
                });

            });
            app.UseMvc();


        }
    }

    public class NewMsgEvent
    { }

    public class NewMsgEventHandler : Hummingbird.Extensions.EventBus.Abstractions.IEventHandler<NewMsgEvent>
    {
        public Task<bool> Handle(NewMsgEvent @event, Dictionary<string, object> headers, CancellationToken cancellationToken)
        {

            Console.WriteLine("handle");
            System.Threading.Thread.Sleep(10);
            return Task.FromResult(true);
        }
    }
}