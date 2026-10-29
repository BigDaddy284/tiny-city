using TinyCity.Simulation.Content;
using TinyCity.Simulation.World;

namespace TinyCity.Art
{
    /// <summary>How a place is decorated: its floor, walls, and, from outside, its building.</summary>
    public enum PlaceKind
    {
        Village,
        Cottage,
        Store,
        Cafe,
        Bakery,
        Clinic,
        Shop,
        Bar,
    }

    public static class PlaceStyle
    {
        /// <summary>The kind of place a map is. Unknown businesses get a plain shop, other interiors a cottage.</summary>
        public static PlaceKind Of(TownWorld world, TileMap map)
        {
            if (map.IsExterior)
            {
                return PlaceKind.Village;
            }

            switch (map.Id)
            {
                case StarterTown.StoreMapId: return PlaceKind.Store;
                case StarterTown.CafeMapId: return PlaceKind.Cafe;
                case StarterTown.BakeryMapId: return PlaceKind.Bakery;
                case StarterTown.ClinicMapId: return PlaceKind.Clinic;
                case StarterTown.BarMapId: return PlaceKind.Bar;
            }

            return world?.BusinessOnMap(map.Id) != null ? PlaceKind.Shop : PlaceKind.Cottage;
        }

        /// <summary>A stable number that picks a cottage's colours.</summary>
        public static int Variant(string mapId) => Noise.Of(mapId);
    }
}
