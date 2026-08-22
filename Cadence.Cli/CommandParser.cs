using Cadence.Core.Interfaces;
using Cadence.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using TaskStatusModel = Cadence.Core.Models.TaskStatus;

namespace Cadence.Cli
{
    public class CommandParser
    {
        private static void PrintHelp()
    {
            Console.WriteLine("Cadence CLI — Daily Routine Manager");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  status                              Show current block and pending tasks");
            Console.WriteLine("  add \"Title\" --container \"Label\"    Add a task to a container");
            Console.WriteLine("  complete [Id]                       Mark a task as completed");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  cadence status");
            Console.WriteLine("  cadence add \"Sketch layout\" --container \"Art\"");
            Console.WriteLine("  cadence add \"Read chapter 3\"                           (uses current block)");
            Console.WriteLine("  cadence complete 4");
    }

        public static async Task RunAsync(string[] args, IServiceProvider services)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }
            var command = args[0].ToLower();    
            switch (command)
            {
                case "status":
                    await StatusAsync(services);
                    break;
                case "add":
                    await AddAsync(args, services);
                    break;
                case "complete":
                    await CompleteAsync(args, services);
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    PrintHelp();
                    break;
            }
        }
        private static async Task StatusAsync(IServiceProvider services)
        {
            var routine = services.GetRequiredService<IRoutineSource>();
            var store = services.GetRequiredService<ICadenceStore>();
            var clock = services.GetRequiredService<IClock>();

            var current = routine.GetCurrentBlock(clock.Now);
            Console.WriteLine($"Current block: {current.Block.Label} (Cycle {current.CycleId})");
            Console.WriteLine($"Started at: {current.Block.StartTime:HH:mm}");

            var tasks = await store.GetTasksByContainerLabelAsync(current.Block.Label, null);
            if (tasks.Count == 0){
                Console.WriteLine("No pending tasks for this block.");
            }
            else
            {
                Console.WriteLine("Pending tasks:");
                foreach (var task in tasks)
                {
                    Console.WriteLine($"  [{task.Id}] {task.Title} (Priority: {task.Priority})");
                }
            }
        }
        private static async Task AddAsync(string[] args, IServiceProvider services)
        {
            // args: add "Title" --container "Label"
            // Parse title: everything between first and last quote, or args[1] if no quotes
            // Parse --container: find flag, take next arg

            var routine = services.GetRequiredService<IRoutineSource>();
            var store = services.GetRequiredService<ICadenceStore>();
            var clock = services.GetRequiredService<IClock>();

            var title = ExtractQuotedTitle(args, 1);
            var container = ExtractContainerLabel(args, "--container");

            if (string.IsNullOrWhiteSpace(container))
            {
                var current = routine.GetCurrentBlock(clock.Now);
                container = current.Block.Label;
            }
            
            var task = new TaskItem
            {
                Title = title,
                ContainerLabel = container,
                Status = TaskStatusModel.Pending,
                Priority = TaskPriority.Normal
            };
            var addedTask = await store.AddTaskAsync(task);
            Console.WriteLine($"Added task [{addedTask.Id}] \"{addedTask.Title}\" to container \"{addedTask.ContainerLabel}\".");
        }

        private static async Task CompleteAsync(string[] args, IServiceProvider services)
{
    var store = services.GetRequiredService<ICadenceStore>();
    var routine = services.GetRequiredService<IRoutineSource>();
    var clock = services.GetRequiredService<IClock>();

    if (args.Length < 2 || !int.TryParse(args[1], out var taskId))
    {
        // No ID given: show pending tasks so the user can pick one
        var current = routine.GetCurrentBlock(clock.Now);
        var tasks = await store.GetTasksByContainerLabelAsync(
            current.Block.Label, status: TaskStatusModel.Pending);

        if (tasks.Count == 0)
        {
            Console.WriteLine("No pending tasks in current block.");
            return;
        }

        Console.WriteLine($"Pending tasks in '{current.Block.Label}':");
        foreach (var task in tasks)
            Console.WriteLine($"  [{task.Id}] {task.Title}");
        Console.WriteLine();
        Console.WriteLine("Usage: complete [Id]");
        return;
    }

    var success = await store.CompleteTaskAsync(taskId);
    Console.WriteLine(success
        ? $"Task {taskId} marked as completed."
        : $"Task {taskId} not found.");
}

        private static string ExtractQuotedTitle(string[] args, int startIndex)
        {
            if (args.Length <= startIndex)
            {
                return string.Empty;
            }

            var title = string.Join(" ", args.Skip(startIndex).TakeWhile(arg => !arg.Equals("--container", StringComparison.OrdinalIgnoreCase)));
            return title.Trim().Trim('"');
        }
        private static string ExtractContainerLabel(string[] args, string flag)
        {
            var flagIndex = Array.IndexOf(args, flag);
            if (flagIndex >= 0 && flagIndex < args.Length - 1)
            {
                return args[flagIndex + 1];
            }

            return string.Empty;
        }
    }
}