namespace TimerApp.Models;

public class Subject
{
    public static readonly Guid GeneralId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>Swatches offered when creating or recolouring a subject.</summary>
    public static readonly string[] Palette =
        ["#F5B14C", "#4FD1C5", "#FF7A6B", "#9B8CFF", "#6CB6FF", "#7BD88F", "#F38BC4", "#C8CDD8"];

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#C8CDD8";
}
