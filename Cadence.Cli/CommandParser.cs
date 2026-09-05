using Cadence.Core.Interfaces;
using Cadence.Core.Models;
using Cadence.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
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
            Console.WriteLine("  start                         Start the background worker process");
            Console.WriteLine("  stop                          Stop the background worker process");
            Console.WriteLine("  status                              Show current block and pending tasks");
            Console.WriteLine("  add \"Title\" --container \"Label\" --priority [Priority]    Add a task to a container");
            Console.WriteLine("  complete [Id]                       Mark a task as completed");
            Console.WriteLine("  modify [Id] \"New Title\" --priority [Priority]   Modify a task's title and/or priority");
            Console.WriteLine("  containers                          List all containers and their pending task counts");
            Console.WriteLine("  heartbeat                           Check if the worker is alive");
            Console.WriteLine("  delete [Id]                         Delete a task by its ID");

            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  cadence status");
            Console.WriteLine("  cadence add \"Sketch layout\" --container \"Art\"");
            Console.WriteLine("  cadence add \"Read chapter 3\"                           (uses current block)");
            Console.WriteLine("  cadence add \"Urgent task\" --priority High");
            Console.WriteLine("  cadence complete 4");
            Console.WriteLine("  cadence modify 4 \"Read chapter 4\"");
            Console.WriteLine("  cadence modify 4 --priority High");
            Console.WriteLine("  cadence modify 4 \"Read chapter 4\" --priority High");
            Console.WriteLine("  cadence delete 4");
            Console.WriteLine("  cadence containers");
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
                case "modify":
                    await ModifyAsync(args, services);
                    break;
                case "containers":
                    await ListContainersAsync(services);
                    break;
                case "heartbeat":
                    await HeartbeatAsync(services);
                    break;
                case "start":
                    StartWorker(services);
                    break;
                case "stop":
                    StopWorker(services);
                    break;
                case "delete":
                    await DeleteAsync(args, services);
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

            var pendingTasks = await store.GetTasksByContainerLabelAsync(
                current.Block.Label, status: TaskStatusModel.Pending);
            var completedTasks = await store.GetTasksByContainerLabelAsync(
                current.Block.Label, status: TaskStatusModel.Completed);
            if (pendingTasks.Count == 0)
            {
                Console.WriteLine("No pending tasks for this block.");
                if (completedTasks.Count > 0)
                {
                    Console.WriteLine("Completed tasks:");
                    foreach (var task in completedTasks)
                    {
                        Console.WriteLine($"  [{task.Id}] {task.Title} (Priority: {task.Priority})");
                    }
                }
            }
            else
            {
                Console.WriteLine("Current tasks:");
                foreach (var task in pendingTasks)
                {
                    Console.WriteLine($"  [{task.Id}] {task.Title} (Priority: {task.Priority})");
                }
                Console.WriteLine("Completed tasks:");
                foreach (var task in completedTasks)
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
            var container = ExtractFlagValue(args, "--container");
            var priorityText = ExtractFlagValue(args, "--priority");
            var hasPriorityFlag = args.Any(arg => arg.Equals("--priority", StringComparison.OrdinalIgnoreCase));
            var priority = TaskPriority.Normal;

            if (hasPriorityFlag &&
                (string.IsNullOrWhiteSpace(priorityText) ||
                !Enum.TryParse(priorityText, ignoreCase: true, out priority)))
            {
                Console.WriteLine($"Invalid priority '{priorityText}'. Valid priorities: Low, Normal, High.");
                Console.WriteLine("Usage: add \"Title\" --container \"Label\" --priority [Priority]");
                return;
            }

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
                Priority = priority
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
                    Console.WriteLine($"  [{task.Id}] {task.Title} (Priority: {task.Priority})");
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

            var title = string.Join(" ", args.Skip(startIndex).TakeWhile(arg =>
                !arg.Equals("--container", StringComparison.OrdinalIgnoreCase) &&
                !arg.Equals("--priority", StringComparison.OrdinalIgnoreCase)));
            return title.Trim().Trim('"');
        }
        private static string ExtractFlagValue(string[] args, string flag)
        {
            var flagIndex = Array.FindIndex(args, arg => arg.Equals(flag, StringComparison.OrdinalIgnoreCase));
            if (flagIndex >= 0 && flagIndex < args.Length - 1)
            {
                return args[flagIndex + 1];
            }

            return string.Empty;
        }
        private static async Task ModifyAsync(string[] args, IServiceProvider services)
        {
            var store = services.GetRequiredService<ICadenceStore>();
            var routine = services.GetRequiredService<IRoutineSource>();
            var clock = services.GetRequiredService<IClock>();

            if (args.Length < 2 || !int.TryParse(args[1], out var taskId))
            {
                var current = routine.GetCurrentBlock(clock.Now);
                var tasks = await store.GetTasksByContainerLabelAsync(
                current.Block.Label, status: null);

                if (tasks.Count == 0)
                {
                    Console.WriteLine("No tasks in current block.");
                    return;
                }
                Console.WriteLine($"Tasks in '{current.Block.Label}':");
                foreach (var task in tasks)
                    Console.WriteLine($"  [{task.Id}] {task.Title} (Priority: {task.Priority})");
                Console.WriteLine();
                Console.WriteLine("Usage: modify [Id] \"New Title\" (optional) --priority [Priority]");
                return;
            }
            var newTitle = ExtractQuotedTitle(args, 2);
            var priorityText = ExtractFlagValue(args, "--priority");
            TaskPriority? newPriority = null;
            var parsedPriority = default(TaskPriority);
            var hasPriorityFlag = args.Any(arg => arg.Equals("--priority", StringComparison.OrdinalIgnoreCase));
            if (hasPriorityFlag &&
                (string.IsNullOrWhiteSpace(priorityText) ||
                !Enum.TryParse(priorityText, ignoreCase: true, out parsedPriority)))
            {
                Console.WriteLine($"Invalid priority '{priorityText}'. Valid priorities: Low, Normal, High.");
                Console.WriteLine("Usage: modify [Id] \"New Title\" --priority [Priority]");
                return;
            }

            if (!string.IsNullOrWhiteSpace(priorityText))
            {
                newPriority = parsedPriority;
            }

            var success = await store.ModifyTaskAsync(
                taskId,
                string.IsNullOrWhiteSpace(newTitle) ? null : newTitle,
                newPriority);
            Console.WriteLine(success
            ? $"Task {taskId} modified."
            : $"Task {taskId} not found.");

        }

        private static async Task ListContainersAsync(IServiceProvider services)
        {
            var routine = services.GetRequiredService<IRoutineSource>();
            var clock = services.GetRequiredService<IClock>();
            var blocks = routine.Blocks;
            var store = services.GetRequiredService<ICadenceStore>();
            var counts = await store.GetContainerTaskCountsAsync();
            var dbLabels = counts.Select(c => c.ContainerLabel).ToHashSet();

            var current = routine.GetCurrentBlock(clock.Now);
            Console.WriteLine($"Current block: {current.Block.Label} (Cycle {current.CycleId})");
            Console.WriteLine("Container task counts:");
            foreach (var block in blocks)
            {
                var count = counts.FirstOrDefault(c => c.ContainerLabel == block.Label)?.PendingCount ?? 0;
                var isCurrent = block.Label == current.Block.Label;
                var marker = isCurrent ? " <= Current Block" : "";
                var time = block.StartTime.ToString("HH:mm");
                Console.WriteLine($"  {block.Label} (Start: {time}) - Pending tasks: {count}{marker}");
            }

            var blockLabels = blocks.Select(b => b.Label).ToHashSet();
            var orphans = dbLabels.Where(l => !blockLabels.Contains(l)).ToList();

            if (orphans.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Orphan containers (not in routine):");
                foreach (var orphan in orphans)
                {
                    var count = counts.FirstOrDefault(c => c.ContainerLabel == orphan)?.PendingCount ?? 0;
                    Console.WriteLine($"  {orphan} - Pending tasks: {count}");
                }
            }

        }
        private static async Task HeartbeatAsync(IServiceProvider services)
        {
            var store = services.GetRequiredService<ICadenceStore>();
            var clock = services.GetRequiredService<IClock>();

            var lastTick = await store.GetLastHeartbeatAsync();
            if (lastTick is null)
            {
                Console.WriteLine("Worker has never ticked.");
                return;
            }

            var elapsed = clock.Now - lastTick.Value;
            var threshold = TimeSpan.FromSeconds(33); // +3 the tick interval

            if (elapsed <= threshold)
            {
                Console.WriteLine($"Worker is alive. Last heartbeat was {elapsed.TotalSeconds:F1} seconds ago.");
            }
            else
            {
                Console.WriteLine($"Worker is down. Last heartbeat was {elapsed.TotalSeconds:F1} seconds ago. threshold : {threshold.TotalSeconds} seconds.");
            }
        }
        private static void StartWorker(IServiceProvider services)
        {
            var workerProjectPath = FindWorkerProject();
            if (workerProjectPath is null)
            {
                Console.WriteLine("Worker project not found.");
                return;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,

                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(workerProjectPath)
            };

            processStartInfo.ArgumentList.Add("run");
            processStartInfo.ArgumentList.Add("--project");
            processStartInfo.ArgumentList.Add(workerProjectPath);
            Process.Start(processStartInfo);
            Console.WriteLine("Worker process started.");
        }

        private static void StopWorker(IServiceProvider services)
        {
            var pidPath = Path.Combine(ServiceCollectionExtensions.GetCadenceDbDirectory(), "worker.pid");
            if (!File.Exists(pidPath))
            {
                Console.WriteLine("Worker is not running (no PID file).");
                return;
            }

            var pidText = File.ReadAllText(pidPath);
            if (!int.TryParse(pidText, out var pid))
            {
                Console.WriteLine("Invalid PID file. Cleaning up.");
                File.Delete(pidPath);
                return;
            }

            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
                Console.WriteLine($"Worker stopped (PID {pid}).");
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Worker process {pid} not found (already stopped).");
            }
            finally
            {
                if (File.Exists(pidPath))
                    File.Delete(pidPath);
            }
        }

        private static string? FindWorkerProject()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 10; i++)
            {
                if (dir == null) break;

                var slnPath = Path.Combine(dir.FullName, "Cadence.sln");
                if (File.Exists(slnPath))
                {
                    var csproj = Path.Combine(dir.FullName, "Cadence.Worker", "Cadence.Worker.csproj");
                    return File.Exists(csproj) ? csproj : null;
                }

                dir = dir.Parent;
            }

            return null;
        }
        private static async Task DeleteAsync(string[] args, IServiceProvider services)
        {
            var store = services.GetRequiredService<ICadenceStore>();

            var routine = services.GetRequiredService<IRoutineSource>();
            var clock = services.GetRequiredService<IClock>();

            if (args.Length < 2 || !int.TryParse(args[1], out var taskId))
            {
                // No ID given: show tasks so the user can pick one
                var current = routine.GetCurrentBlock(clock.Now);
                var tasks = await store.GetTasksByContainerLabelAsync(
                current.Block.Label);

                if (tasks.Count == 0)
                {
                    Console.WriteLine("No tasks in current block.");
                    return;
                }

                Console.WriteLine($"Tasks in '{current.Block.Label}':");
                foreach (var task in tasks)
                    Console.WriteLine($"  [{task.Id}] {task.Title}");
                Console.WriteLine();
                Console.WriteLine("Usage: complete [Id]");
                return;
            }

            var success = await store.DeleteTaskAsync(taskId);
            Console.WriteLine(success
                ? $"Task {taskId} deleted."
                : $"Task {taskId} not found.");
        }
    }
}