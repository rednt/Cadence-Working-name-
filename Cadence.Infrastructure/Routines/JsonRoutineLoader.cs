using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cadence.Core.Models;
namespace Cadence.Infrastructure.Routines
{
    public sealed class JsonRoutineLoader
    {
        private sealed class RoutineFileDto
        {
            public string? Profile { get; set;} 
            public List<BlockDto>? Blocks { get; set; }
        }
        private sealed class BlockDto
        {
            public string? Label { get; set; }
            public BlockRole? Role { get; set; }
            public string? Time { get; set; }
        }

        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter<BlockRole>(JsonNamingPolicy.CamelCase, allowIntegerValues: false ) }
        };

        public IReadOnlyList<Block> Parse(string json)
        {
            var dto = JsonSerializer.Deserialize<RoutineFileDto>(json, _options) ?? throw new InvalidOperationException("Failed to deserialize routine file.");
            if (dto.Blocks is null || dto.Blocks.Count == 0)
            {
                throw new InvalidOperationException("Routine file contains no blocks.");
            }

            var blocks = dto.Blocks.Select(b => new Block(
            TimeOnly.Parse(b.Time ?? throw new InvalidOperationException("Block time is missing."), CultureInfo.InvariantCulture),
            b.Label ?? string.Empty,
            b.Role ?? BlockRole.Unspecified)).ToList();          

            var duplicate = blocks.GroupBy(b => b.StartTime).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)                               
            {
                throw new InvalidOperationException($"Duplicate block start time '{duplicate.Key:HH:mm}' - start times must be unique.");
            }   

            return blocks;                                           
        }
        public IReadOnlyList<Block> Load(string path)=> Parse(File.ReadAllText(path));
        
    }
    
}   