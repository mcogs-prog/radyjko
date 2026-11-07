namespace RADYJKO;

public class Station
{
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public bool IsCustom { get; set; }

    public Station() { }
    public Station(string name, string streamUrl, string genre, bool isCustom = false)
    {
        Name = name; StreamUrl = streamUrl; Genre = genre; IsCustom = isCustom;
    }
}
