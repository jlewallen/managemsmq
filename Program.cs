using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Messaging;

namespace ManageQueues
{
  public class Program
  {
    public static void Main(string[] args)
    {
      if (!args.Any())
      {
        Help();
        return;
      }
      var move = args.Any(arg => arg == "--move");
      if (move)
      {
        Move(args);
        return;
      }

      var delete = args.Any(arg => arg == "--delete");
      var recreate = args.Any(arg => arg == "--recreate");
      var purge = args.Any(arg => arg == "--purge");
      Action<string> action = CreateIfMissing;
      if (purge) action = Purge;
      if (recreate) action = DeleteAndCreate;
      if (delete) action = Delete;
      var files = args.Where(File.Exists);
      var otherArgs = args.Where(arg => !arg.StartsWith("--")).Except(files);
      var paths = files.SelectMany(file => File.ReadAllLines(file).
                        Select(line => line.Trim())).
                        Where(line => line.Length > 0);
      foreach (var queuePath in paths.Union(otherArgs))
      {
        try
        {
          action(queuePath);
        }
        catch (Exception error)
        {
          Console.WriteLine("Error: " + error.Message);
        }
      }
    }
    
    static void Help()
    {
      Console.WriteLine("--delete <file|queue-name>+");
      Console.WriteLine("--purge <file|queue-name>+");
      Console.WriteLine("--recreate <file|queue-name>+");
      Console.WriteLine("--move <from-queue-name> <to-queue-name> <number-messages-to-move>");
    }

    static void Move(IEnumerable<string> args)
    {
      var maximum = args.FirstNumberOr(1);
      var sourceQueue = args.NonIntegerArgs().Skip(1).First().ToQueueName();
      var destinyQueue = args.NonIntegerArgs().Last().ToQueueName();
      var source = new MessageQueue(sourceQueue, QueueAccessMode.SendAndReceive);
      var destiny = new MessageQueue(destinyQueue, QueueAccessMode.SendAndReceive);
      Console.WriteLine("Moving " + maximum + " messages from " + sourceQueue + " to " + destinyQueue);
      while (maximum > 0)
      {
        var message = source.FastReceive(MessageQueueTransactionType.Automatic);
        if (message == null)
        {
          Console.WriteLine("No more messages");
          break;
        }
        Console.WriteLine("Moving " + message.Id + " " + sourceQueue + " to " + destinyQueue);
        destiny.Send(message, MessageQueueTransactionType.Single);
        maximum--;
      }
    }

    static void Purge(string path)
    {
      if (!MessageQueue.Exists(path)) return;
      Console.WriteLine("Purging {0}", path);
      var queue = new MessageQueue(path);
      queue.Purge();
    }

    static void CreateIfMissing(string path)
    {
      if (!MessageQueue.Exists(path))
      {
        Console.WriteLine("Creating {0}", path);
        var queue = MessageQueue.Create(path, true);
        queue.SetPermissions("Network Service", MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow);
      }
      else
      {
        Console.WriteLine("Exists {0}", path);
      }
    }

    static void Delete(string path)
    {
      if (MessageQueue.Exists(path))
      {
        Console.WriteLine("Deleting {0}", path);
        MessageQueue.Delete(path);
      }
    }

    static void DeleteAndCreate(string path)
    {
      if (MessageQueue.Exists(path))
      {
        Console.WriteLine("Deleting {0}", path);
        MessageQueue.Delete(path);
      }
      Console.WriteLine("Creating {0}", path);
      var queue = MessageQueue.Create(path, true);
      queue.SetPermissions("Network Service", MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow);
    }
  }

  public static class QueueHelpers
  {
    public static Message FastReceive(this MessageQueue queue, MessageQueueTransactionType transactionType)
    {
      try
      {
        return queue.Receive(TimeSpan.FromSeconds(1.0), transactionType);
      }
      catch (MessageQueueException error)
      {
        if (error.MessageQueueErrorCode == MessageQueueErrorCode.IOTimeout)
        {
          return null;
        }
        throw;
      }
    }

    public static string ToQueueName(this string value)
    {
      if (value.StartsWith(@".\private$\"))
        return value;
      return @".\private$\" + value;
    }
  }

  public static class NumberHelpers
  {
    public static Int32 FirstNumberOr(this IEnumerable<string> values, Int32 defaultValue)
    {
      foreach (var arg in values)
      {
        var parsed = 0;
        if (Int32.TryParse(arg, out parsed))
        {
          return parsed;
        }
      }
      return defaultValue;
    }

    public static bool IsInteger(this string value)
    {
      var parsed = 0;
      return Int32.TryParse(value, out parsed);
    }
  }

  public static class ArgHelpers
  {
    public static IEnumerable<string> NonIntegerArgs(this IEnumerable<string> values)
    {
      return values.Where(arg => !arg.IsInteger());
    }
  }
}
