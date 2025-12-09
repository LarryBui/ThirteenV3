using UnityEngine;
using Serilog; 
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display; // Added
using System.IO;

namespace Serilog.Sinks.Unity3D
{
   public class Unity3DSink : ILogEventSink
   {
      private readonly ITextFormatter _textFormatter;

      public Unity3DSink(ITextFormatter textFormatter)
      {
         _textFormatter = textFormatter;
      }

      public void Emit(LogEvent logEvent)
      {
         if (logEvent == null) return;

         using (var buffer = new StringWriter())
         {
            _textFormatter.Format(logEvent, buffer);
            string message = buffer.ToString().Trim();

            if (logEvent.Exception != null)
               message += $"\n{logEvent.Exception}";

            if (logEvent.Level == LogEventLevel.Error || logEvent.Level == LogEventLevel.Fatal)
               Debug.LogError(message);
            else if (logEvent.Level == LogEventLevel.Warning)
               Debug.LogWarning(message);
            else
               Debug.Log(message);
         }
      }
   }

   public static class Unity3DLoggerConfigurationExtensions
   {
      public static LoggerConfiguration Unity3D(
         this LoggerSinkConfiguration sinkConfiguration,
         string outputTemplate = "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
      {
         var formatter = new Serilog.Formatting.Display.MessageTemplateTextFormatter(outputTemplate, null);
         return sinkConfiguration.Sink(new Unity3DSink(formatter));
      }
   }
}