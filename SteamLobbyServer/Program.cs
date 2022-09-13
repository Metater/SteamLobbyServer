using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

Random random = new();
object l = new();
Dictionary<string, List<Lobby>> gameLobbies = new()
{
    { "MotorBall", new() }
};

// http://localhost:5129/create?Game=MotorBall&IsPublic=true&Title=Test%20Game&Host=420

app.MapGet("/create", (string Game, bool IsPublic, string Title, ulong Host) =>
{
    Title = Title.Trim();

    string response = "error";
    if (Game.Length > 32)
    {
        response += ";game > 32 chars";
    }
    else if (Title.Length > 16)
    {
        response += ";title > 16 chars";
    }
    else if (!Regex.IsMatch(Title, "^[a-zA-Z0-9 ]+$"))
    {
        response += ";title contains invalid chars";
    }
    else
    {
        lock (l)
        {
            if (gameLobbies.TryGetValue(Game, out var game))
            {
                game.RemoveAll(l => l.Host == Host);
                response = GetKey();
                while (game.Any(l => l.Key == response))
                {
                    response = GetKey();
                }
                var lobby = new Lobby(IsPublic, Title, Host, response);
                game.Add(lobby);
                response += $";{lobby.Token}";
            }
            else
            {
                response += ";game not found";
            }
        }
    }
    Console.WriteLine(
        $"/create\n" +
        $"inputs:\n" +
        $"\tGame: {Game}\n" +
        $"\tIsPublic: {IsPublic}\n" +
        $"\tTitle: {Title}\n" +
        $"\tHost: {Host}\n" +
        $"output:\n" +
        $"\t{response}\n"
    );
    return response;
});

app.MapGet("/read", (string Game, string Key) =>
{
    string response = "error";
    if (Game.Length > 32)
    {
        response += ";game > 32 chars";
    }
    else if (Key.Length > 32)
    {
        response += ";key > 32 chars";
    }
    else
    {
        lock (l)
        {
            if (gameLobbies.TryGetValue(Game, out var game))
            {
                var lobby = game.Find(l => l.Key == Key);
                if (lobby is not null)
                {
                    response = lobby.Host.ToString();
                }
                else
                {
                    response += ";lobby not found";
                }
            }
            else
            {
                response += ";game not found";
            }
        }
    }
    Console.WriteLine(
        $"/read\n" +
        $"inputs:\n" +
        $"\tGame: {Game}\n" +
        $"\tKey: {Key}\n" +
        $"output:\n" +
        $"\t{response}\n"
    );
    return response;
});

app.MapGet("/list", (string Game) =>
{
    string response = "error";
    if (Game.Length > 32)
    {
        response += ";game > 32 chars";
    }
    else
    {
        lock (l)
        {
            if (gameLobbies.TryGetValue(Game, out var game))
            {
                var lobbies = game.FindAll(l => l.IsPublic);
                if (lobbies is not null)
                {
                    StringBuilder sb = new();
                    foreach (var lobby in lobbies)
                    {
                        sb.AppendLine(lobby.ToString());
                    }
                    response = sb.ToString();
                }
                else
                {
                    response += ";no lobbies found";
                }
            }
            else
            {
                response += ";game not found";
            }
        }
    }
    Console.WriteLine(
        $"/list\n" +
        $"inputs:\n" +
        $"\tGame: {Game}\n" +
        $"output:\n" +
        $"{response}\n"
    );
    return response;
});

app.MapGet("/heartbeat", (string Game, Guid Token) =>
{
    string response = "error";
    if (Game.Length > 32)
    {
        response += ";game > 32 chars";
    }
    else
    {
        lock (l)
        {
            if (gameLobbies.TryGetValue(Game, out var game))
            {
                var lobby = game.Find(l => l.Token == Token);
                if (lobby is not null)
                {
                    lobby.Heartbeat();
                    response = "ok";
                }
                else
                {
                    response += ";lobby not found";
                }
            }
            else
            {
                response += ";game not found";
            }
        }
    }
    Console.WriteLine(
        $"/heartbeat\n" +
        $"inputs:\n" +
        $"\tGame: {Game}\n" +
        $"\tToken: {Token}\n" +
        $"output:\n" +
        $"\t{response}\n"
    );
    return response;
});

List<Task> tasks = new();
tasks.Add(app.RunAsync());
tasks.Add(Task.Run(async () =>
{
    while (true)
    {
        lock (l)
        {
            foreach ((var name, var game) in gameLobbies)
            {
                int lobbiesRemoved = game.RemoveAll(l => !l.IsLastHeartbeatWithin(2));
                if (lobbiesRemoved > 0)
                {
                    Console.WriteLine($"removed {lobbiesRemoved} lobbies from {name}");
                }
            }
        }
        await Task.Delay(TimeSpan.FromMinutes(1));
    }
}));
await Task.WhenAll(tasks);

string GetKey()
{
    return random.Next(0, 1000).ToString();
}

record Lobby(bool IsPublic, string Title, ulong Host, string Key)
{
    public Guid Token { get; } = Guid.NewGuid();
    private DateTime lastHeartbeat = DateTime.Now;

    public override string ToString()
    {
        return $"Title: {Title}, Host: {Host}, Key: {Key}";
    }

    public void Heartbeat()
    {
        lastHeartbeat = DateTime.Now;
    }

    public bool IsLastHeartbeatWithin(double minutes)
    {
        return (DateTime.Now - lastHeartbeat) < TimeSpan.FromMinutes(minutes);
    }
}