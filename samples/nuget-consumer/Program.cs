using Newtonsoft.Json;

var payload = JsonConvert.SerializeObject(new { source = "heimdall", ok = true });
Console.WriteLine(payload);
