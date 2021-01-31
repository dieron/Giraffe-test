module firstGiraffe.App

open System
open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Http
open Giraffe
open System.Collections.Generic

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string;
        Messages : string list;
    }

[<CLIMutable>]
type AddMessage =
    {
        Message : string;
    }


type Storage() = 
    static let messages = new List<string>()
    member this.Messages = messages


// ---------------------------------
// Views
// ---------------------------------

module Views =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "firstGiraffe" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "Giraffe powered web site" ]

    let index (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
            p [] [ encodedText "Add some comment" ]
            p [] [ 
                div [] [
                    form [_name "Main"; _method "post"; _action "addm"] [
                        encodedText "Message: " 
                        rawText "&nbsp;" 
                        input [_name "message"; _size "100"; _maxlength "200"; ]
                        rawText "&nbsp;" 
                        button [_type "submit"; _name "submitButton"; _value "Add"; ] [encodedText "Add"]
                    ]
                ]
            ]
            p [] [
                p [] [ encodedText "Last 10 messages" ]
                ul [] [
                    yield!
                        model.Messages
                        |> List.map (fun b -> li [] [ encodedText b ])
                ]
            ]
        ] |> layout

// ---------------------------------
// Web app
// ---------------------------------

let indexHandler () : HttpHandler =
    let greetings = "Hello from Giraffe! "
    let storage = new Storage()
    let topMessages = if storage.Messages.Count > 10 then storage.Messages |> Seq.take 10 |> Seq.toList else storage.Messages |> Seq.toList
    let model     = { Text = greetings; Messages = topMessages; }
    let view      = Views.index model
    htmlView view

let addMessageHandler : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! model = ctx.BindFormAsync<AddMessage>()
            let storage = new Storage()
            
            storage.Messages.Insert (0, model.Message.Substring(0, (Math.Min(200, model.Message.Length))))

            return! redirectTo false "/" next ctx
        }



let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> warbler (fun _ -> (indexHandler()) >=> noResponseCaching)
            ]
        POST >=>
            choose [
                route "/addm" >=> warbler (fun _ -> addMessageHandler >=> noResponseCaching)
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:5000")
       .AllowAnyMethod()
       .AllowAnyHeader()
       |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app .UseGiraffeErrorHandler(errorHandler)
    )
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    //services.AddResponseCaching()    |> ignore
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    .UseUrls("http://0.0.0.0:5000/")
                    |> ignore)
        .Build()
        .Run()
    0