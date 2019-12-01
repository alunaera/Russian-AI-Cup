using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        static double DistanceSqr(Vec2Double a, Vec2Double b)
        {
            return (a.X - b.X) * (a.X - b.X) + (a.Y - b.X) * (a.Y - b.Y);
        }

        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            Unit? nearestEnemy = null;
            foreach (var other in game.Units)
            {
                if (other.PlayerId != unit.PlayerId)
                {
                    if (!nearestEnemy.HasValue || DistanceSqr(unit.Position, other.Position) <
                       DistanceSqr(unit.Position, nearestEnemy.Value.Position))
                    {
                        nearestEnemy = other;
                    }
                }
            }

            LootBox? nearestWeapon = null;
            foreach (var lootBox in game.LootBoxes)
            {
                if (lootBox.Item is Item.HealthPack && unit.Health < 50)
                {
                    if (!nearestWeapon.HasValue || DistanceSqr(unit.Position, lootBox.Position) <
                        DistanceSqr(unit.Position, nearestWeapon.Value.Position))
                    {
                        nearestWeapon = lootBox;
                    }
                }
            }

            Vec2Double targetPos = unit.Position;
            if (!unit.Weapon.HasValue && nearestWeapon.HasValue)
            {
                targetPos = nearestWeapon.Value.Position;
            }
            else if (nearestEnemy.HasValue)
            {
                targetPos = nearestEnemy.Value.Position;
            }
            debug.Draw(new CustomData.Log("Target pos: " + targetPos));

            Vec2Double aim = new Vec2Double(0, 0);
            if (nearestEnemy.HasValue)
            {
                aim = new Vec2Double(nearestEnemy.Value.Position.X - unit.Position.X,
                    nearestEnemy.Value.Position.Y - unit.Position.Y);
            }

            bool jump = targetPos.Y > unit.Position.Y ||
                        targetPos.X > unit.Position.X &&
                        game.Level.Tiles[(int)(unit.Position.X + 1)][(int)unit.Position.Y] == Tile.Wall ||
                        targetPos.X < unit.Position.X &&
                        game.Level.Tiles[(int)(unit.Position.X - 1)][(int)(unit.Position.Y)] == Tile.Wall;

            bool shoot = targetPos.X < unit.Position.X
                ? game.Level.Tiles[(int) (targetPos.X) - 1][(int) targetPos.Y] != Tile.Wall
                : game.Level.Tiles[(int) (targetPos.X) + -1][(int) targetPos.Y] != Tile.Wall;

            double velocity;

            if (nearestEnemy != null && nearestEnemy.Value.Position.X - unit.Position.X == 0)
                velocity = targetPos.X - unit.Position.X;
            else
            {
                velocity = unit.Position.X - nearestEnemy.Value.Position.X;
                jump = true;
            }

            if (unit.Health < 50 && nearestWeapon != null)
            {
                velocity = nearestWeapon.Value.Position.X - unit.Position.X;
                jump = true;
            }

            UnitAction action = new UnitAction
            {
                Velocity = velocity,
                Jump = jump,
                JumpDown = !jump,
                Aim = aim,
                Shoot = shoot,
                SwapWeapon = false,
                PlantMine = false
            };

            return action;
        }
    }
}