using Application.Discord.Interactions.Command;

namespace Modules.Utility.UserInfo;

internal sealed class UserInfoCommand : ICommandDefinition
{
    public string Name => "userinfo";

    public string Description => "Get information about a user.";

    public IReadOnlyList<CommandOptionDefinition> Options => [
        new (
            Name: "user",
            Description: "The user to get information about.",
            Type: CommandOptionType.User,
            Required: false)
    ];
}
