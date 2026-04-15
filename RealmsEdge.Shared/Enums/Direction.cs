namespace RealmsEdge.Shared.Enums
{
    public enum Direction
    {
        // =====================
        // Cardinal
        // =====================

        North,
        South,
        East,
        West,

        // =====================
        // Diagonal
        // =====================

        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest,

        // =====================
        // Vertical
        // =====================

        Up,                 // Stairs up, ladder up, climb
        Down,               // Stairs down, ladder down, descend

        // =====================
        // Special
        // =====================

        Enter,              // Enter a building or structure
        Exit,               // Leave a building or structure
        Portal              // Magic portal to another location
    }
}