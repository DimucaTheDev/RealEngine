namespace RE.Core.Scripting;

public class CommandNotFoundException(string command) : Exception($"Command '{command}' is not registered");