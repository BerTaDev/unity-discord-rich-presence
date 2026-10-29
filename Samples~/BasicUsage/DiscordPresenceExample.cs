using BerTaDev.DiscordRichPresence;
using UnityEngine;

/// <summary>
/// Minimal example: add this to any GameObject, paste your Application ID and press Play.
/// Use the on-screen buttons to update the presence and watch your Discord profile change.
/// </summary>
public class DiscordPresenceExample : MonoBehaviour
{
    [SerializeField] string applicationId = "YOUR_APPLICATION_ID";

    int floor = 1;

    void Start()
    {
        DiscordPresence.Connected += OnConnected;
        DiscordPresence.Error += OnError;

        DiscordPresence.Initialize(applicationId);
        DiscordPresence.Set(new RichPresence()
            .WithDetails("Exploring the dungeon")
            .WithState("Floor " + floor)
            .WithLargeImage("logo", "My Awesome Game")
            .WithParty(1, 4)
            .WithElapsedTime()
            .WithButton("Play for free", "https://example.com"));
    }

    void OnDestroy()
    {
        DiscordPresence.Connected -= OnConnected;
        DiscordPresence.Error -= OnError;
    }

    void OnConnected(DiscordUser user) => Debug.Log("Hello, " + user.DisplayName + "! Avatar: " + user.GetAvatarUrl());

    void OnError(DiscordError error) => Debug.LogWarning("Discord says: " + error.Message);

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 260, 200), GUI.skin.box);
        GUILayout.Label(DiscordPresence.IsConnected
            ? "Connected as " + DiscordPresence.User.DisplayName
            : "Waiting for Discord...");

        if (GUILayout.Button("Next floor"))
        {
            floor++;
            DiscordPresence.Update(p => p.State = "Floor " + floor);
        }

        if (GUILayout.Button("Boss fight (5 min countdown)"))
            DiscordPresence.Update(p => p.WithDetails("Fighting the boss").WithCountdown(System.TimeSpan.FromMinutes(5)));

        if (GUILayout.Button("Clear presence"))
            DiscordPresence.Clear();

        GUILayout.EndArea();
    }
}
