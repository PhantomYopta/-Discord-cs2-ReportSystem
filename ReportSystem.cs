using System.Text;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;

namespace ReportSystem;

public class ReportSystem : BasePlugin
{
    public override string ModuleName { get; }
    public override string ModuleVersion { get; }

    private Config _config;
    
    public override void Load(bool hotReload)
    {
        _config = LoadConfig();
        AddCommand("css_report", "", (controller, info) =>
        {
            if(controller == null) return;
            
            var reportMenu = new ChatMenu("Report");
            reportMenu.MenuOptions.Clear();
            foreach (var player in Utilities.GetPlayers())
            {
                if(player.IsBot || player.PlayerName == controller.PlayerName) continue;
                
                reportMenu.AddMenuOption($"{player.PlayerName} [{player.EntityIndex!.Value.Value}]", HandleMenu);
            }
            
            ChatMenus.OpenMenu(controller, reportMenu);
        });
    }

    private void HandleMenu(CCSPlayerController controller, ChatMenuOption option)
    {
        var parts = option.Text.Split('[', ']');
        var lastPart = parts[parts.Length - 2];
        var numbersOnly = string.Join("", lastPart.Where(char.IsDigit));
        
        var index = int.Parse(numbersOnly.Trim());
        var reason = File.ReadAllLines(Path.Combine(ModuleDirectory, "reasons.txt"));
        var reasonMenu = new ChatMenu("Reasons");
        reasonMenu.MenuOptions.Clear();
        foreach (var a in reason)
        {
            reasonMenu.AddMenuOption($"{a} [{index}]", HandleMenu2);
        }
            
        ChatMenus.OpenMenu(controller, reasonMenu);
    }

    private void HandleMenu2(CCSPlayerController controller, ChatMenuOption option)
    {
        var parts = option.Text.Split('[', ']');
        var lastPart = parts[parts.Length - 2];
        var numbersOnly = string.Join("", lastPart.Where(char.IsDigit));
        
        var target = Utilities.GetPlayerFromIndex(int.Parse(numbersOnly.Trim()));
        
        Task.Run(() => SendMessageToDiscord(controller.PlayerName, controller.SteamID.ToString(), target.PlayerName,
            target.SteamID.ToString(), parts[0]));
    }

    private async void SendMessageToDiscord(string clientName, string clientSteamId, string targetName,
        string targetSteamId, string msg)
    {
        try
        {
            var webhookUrl = GetWebhook();

            if (string.IsNullOrEmpty(webhookUrl)) return;

            var httpClient = new HttpClient();

            if (msg == "") return;

            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "Report",
                        description = $"{ConVar.Find("hostname")!.StringValue}\n{ConVar.Find("ip")!.StringValue}:{ConVar.Find("hostport")!.GetPrimitiveValue<int>()}",
                        color = 16711680, //255204255
                        fields = new[]
                        {
                            new
                            {
                                name = "Dispatcher",
                                value =
                                    $"**Name:** {clientName}\n**SteamID:** {new SteamID(ulong.Parse(clientSteamId)).SteamId2}\n**Link:** [steam account link](https://steamcommunity.com/profiles/{clientSteamId}/)",
                                inline = false
                            },
                            new
                            {
                                name = "Victim",
                                value =
                                    $"**Name:** {targetName}\n**SteamID:** {new SteamID(ulong.Parse(targetSteamId)).SteamId2}\n**Link:** [steam account link](https://steamcommunity.com/profiles/{targetSteamId}/)",
                                inline = false
                            },
                            new
                            {
                                name = "Reason",
                                value = msg,
                                inline = false
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(webhookUrl, content);

            Console.ForegroundColor = response.IsSuccessStatusCode ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(response.IsSuccessStatusCode
                ? "Success"
                : $"Error: {response.StatusCode}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        
    }
    private Config LoadConfig()
    {
        var configPath = Path.Combine(ModuleDirectory, "settings.json");

        if (!File.Exists(configPath)) return CreateConfig(configPath);

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath))!;

        return config;
    }

    private Config CreateConfig(string configPath)
    {
        var config = new Config
        {
            WebhookUrl = ""
        };

        File.WriteAllText(configPath,
            JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));

        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.WriteLine("[MapChooser] The configuration was successfully saved to a file: " + configPath);
        Console.ResetColor();

        return config;
    }
    private string GetWebhook()
    {
        return _config.WebhookUrl;
    }
}
public class Config
{
    public string WebhookUrl { get; set; }
}