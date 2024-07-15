namespace tower_defense
{
    namespace prefabs
    {
        public struct Enemy { }
        public struct Tree
        {
            public float height;
            public float variation;
        }

        public struct Path { }
        public struct Tile { }

        public class materials
        {
            public struct Metal { }
            public struct CannonHead { }
        }

        public struct Turret
        {
            public struct Base { }
            public struct Head { }
        };

        public struct Cannon
        {
            public struct Head
            {
                public struct BarrelLeft { }
                public struct BarrelRight { }
            }
        }
    }
}