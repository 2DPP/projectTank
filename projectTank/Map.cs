using System.Collections.Generic; // Provides List<T> for storing multiple objects
using System.Drawing; // Provides graphics-related classes like Rectangle, Brush, Graphics

// Wall.cs / Map.cs
// Wall represents a single rectangular obstacle with a color and a Draw() method.
// Map holds all walls in a list and provides AddWall(), IsColliding(), and Draw().
// IsColliding() is used by tanks, bullets, and the AI to check for wall overlap.

public class Wall // Represents a wall object in the game
{
    public Rectangle Bounds { get; private set; }
    // Stores the position and size of the wall; can only be modified inside this class

    public Brush Color { get; private set; }
    // Stores the color used to draw the wall; also restricted to this class

    public Wall(Rectangle bounds, Brush color = null)
    {
        Bounds = bounds; // Assigns the wall's position and size

        Color = color ?? Brushes.DarkSlateGray;
        // If no color is provided, use DarkSlateGray as the default color
    }

    public void Draw(Graphics g)
    {
        g.FillRectangle(Color, Bounds);
        // Fills the wall rectangle with its color

        g.DrawRectangle(Pens.Black, Bounds);
        // Draws a black border around the wall
    }
}

public class Map // Represents the game map containing walls
{
    public List<Wall> Walls { get; private set; } = new List<Wall>();
    // A list that stores all walls in the map

    public int Width { get; private set; }
    // Width of the map

    public int Height { get; private set; }
    // Height of the map

    public Map(int width, int height)
    {
        Width = width; // Sets the map width
        Height = height; // Sets the map height
    }

    public void AddWall(Rectangle rect, Brush color = null)
    {
        Walls.Add(new Wall(rect, color));
        // Creates a new wall and adds it to the list
    }

    // Checks if a rectangle collides with any wall
    public bool IsColliding(Rectangle rect)
    {
        foreach (var wall in Walls) // Loop through all walls
            if (rect.IntersectsWith(wall.Bounds)) // Check if rectangles overlap
                return true; // Collision detected

        return false; // No collision found
    }

    public void Clear()
    {
        Walls.Clear(); // Removes all walls from the map
    }

    public void Draw(Graphics g)
    {
        foreach (var wall in Walls) // Loop through all walls
            wall.Draw(g); // Draw each wall
    }
}