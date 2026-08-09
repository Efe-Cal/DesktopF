namespace DesktopF;

public sealed record DesktopItem(
    string Name,
    string? Path,
    string ParsingName,
    int ViewX,
    int ViewY,
    int ScreenX,
    int ScreenY,
    bool HasScreenPosition,
    int ScreenWidth,
    int ScreenHeight,
    bool HasScreenBounds);
