using System.Collections.Generic;
using System.Drawing;

public class Wall
{
    public Rectangle Bounds { get; private set; }
    public Brush Color { get; private set; }

    public Wall(Rectangle bounds, Brush color = null)
    {
        Bounds = bounds;
        Color = color ?? Brushes.DarkSlateGray;
    }

    public void Draw(Graphics g)
    {
        g.FillRectangle(Color, Bounds);
        g.DrawRectangle(Pens.Black, Bounds);
    }
}

public class Map
{
    public List<Wall> Walls { get; private set; } = new List<Wall>();
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Map(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public void AddWall(Rectangle rect, Brush color = null)
    {
        Walls.Add(new Wall(rect, color));
    }

    // Checks if a rectangle collides with any wall
    public bool IsColliding(Rectangle rect)
    {
        foreach (var wall in Walls)
            if (rect.IntersectsWith(wall.Bounds))
                return true;
        return false;
    }

    public void Clear()
    {
        Walls.Clear();
    }

    public void Draw(Graphics g)
    {
        foreach (var wall in Walls)
            wall.Draw(g);
    }
}