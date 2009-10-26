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
      var files = args.Where(File.Exists);
      var delete = args.Any(arg => arg == "--delete");
      var recreate = args.Any(arg => arg == "--recreate");
      var purge = args.Any(arg => arg == "--purge");
      Action<string> action = CreateIfMissing;
      if (purge) action = Purge;
      if (recreate) action = DeleteAndCreate;
      if (delete) action = Delete;
      var paths = files.SelectMany(file => File.ReadAllLines(file).
                        Select(line => line.Trim())).
                        Where(line => line.Length > 0);
      foreach (var path in paths)
      {
        try
        {
          action(path);
        }
        catch (Exception error)
        {
          Console.WriteLine("Error: " + error.Message);
        }
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
        MessageQueue.Create(path, true);
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
      MessageQueue.Create(path, true);
    }
  }
}
