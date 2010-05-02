using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Xml.Serialization;
using System.IO;
using System.Xml;
using SlimDX.Direct3D9;

namespace CubeHags.client.gfx
{
    public class Tile
    {
        [XmlElement("TileName")]
        public string Name;
        [XmlElement("TileRect")]
        public Rectangle Rect;

        public override string ToString()
        {
            return Name;
        }
    }

    // Texture with subtextures
    public class HagsAtlas : IResettable
    {
        [XmlIgnore()]
        public HagsTexture Texture { get; private set; }
        [XmlElement("TextureName")]
        public string TextureName { get; set; }
        [XmlElement("Tiles")]
        public List<Tile> Tiles = new List<Tile>();
        public string Name { get; set; }

        // Tile accessor
        public Rectangle this[string name]
        {
            get
            {
                foreach (Tile tile in Tiles)
                {
                    if (tile.Name.Equals(name))
                        return tile.Rect;
                }
                throw new ArgumentException("Tile does not exist");
            }
            set
            {
                foreach (Tile tile in Tiles)
                {
                    if (tile.Name.Equals(name))
                    {
                        tile.Rect = value;
                        return;
                    }
                }
                Tiles.Add(new Tile() { Name = name, Rect = value });
            }
        }

        HagsAtlas()
        {

        }

        // Create new HagsAtlas from texture
        public HagsAtlas(string filename)
        {
            Texture = new HagsTexture(filename);
            TextureName = filename;
            Name = Path.GetFileNameWithoutExtension(filename);
        }

        // Add new tile
        public void AddTile(string name, Rectangle rect)
        {
            foreach (Tile tile in Tiles)
            {
                if(tile.Name.Equals(name))
                    throw new ArgumentException("Tile name is already in use");
            }
            this[name] = rect;
        }

        // Remove tile
        public void RemoveTile(string name)
        {
            foreach (Tile tile in Tiles)
            {
                if (tile.Name.Equals(name))
                {
                    Tiles.Remove(tile);
                    return;
                }
            }
            throw new ArgumentException("Could not remove tile. Tile not found");
        }

        // Modify existing tile
        public void ModifyTile(string name, Rectangle rect)
        {
            this[name] = rect;
        }

        // Serialize
        public static void SerializeToXML(HagsAtlas atlas)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(HagsAtlas));
            TextWriter textWriter = new StreamWriter(@"client/data/gui/Atlas/" + atlas.Name + ".xml");
            serializer.Serialize(textWriter, atlas);
            textWriter.Close();
        }

        // De-serialize
        public static HagsAtlas DeserializeFromXML(string filename)
        {
            if (File.Exists(@"client/data/gui/Atlas/" + filename))
            {
                XmlSerializer deserializer = new XmlSerializer(typeof(HagsAtlas));
                TextReader reader = new StreamReader(@"client/data/gui/Atlas/" + filename);
                HagsAtlas atlas = (HagsAtlas)deserializer.Deserialize(reader);
                reader.Close();
                return atlas;
            }
            else
                throw new FileNotFoundException(@"client/data/gui/Atlas/" + filename);
        }

        public SlimDX.Result OnLostDevice()
        {
            return Texture.OnLostDevice();
        }

        public SlimDX.Result OnResetDevice()
        {
            return Texture.OnResetDevice();
        }
    }
}
