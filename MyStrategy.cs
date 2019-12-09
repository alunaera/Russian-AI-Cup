using System;
using System.Linq;
using AiCup2019.Model;

namespace AiCup2019
{
    public class MyStrategy
    {
        static double DistanceSqr(Vec2Double a, Vec2Double b)
        {
            return (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        }

        static double GetVelocity(double start, double finish, int offset)
        {
            return start - finish + Math.Sign(start - finish) * offset;
        }

        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            Unit? nearestEnemy = game.Units
                .Where(otherUnit => otherUnit.PlayerId != unit.PlayerId)
                .OrderBy(otherUnit => DistanceSqr(unit.Position, otherUnit.Position))
                .Select(otherUnit => (Unit?) otherUnit)
                .FirstOrDefault();

            bool swapWeapon = false;
            LootBox? nearestWeapon = null;
            foreach (var lootBox in game.LootBoxes)
            {
                if (lootBox.Item is Item.HealthPack && unit.Health != 100)
                {
                    if (!nearestWeapon.HasValue || DistanceSqr(unit.Position, lootBox.Position) <
                        DistanceSqr(unit.Position, nearestWeapon.Value.Position))
                    {
                        nearestWeapon = lootBox;
                    }
                }

                if (!unit.Weapon.HasValue)
                {
                    if (lootBox.Item is Item.Weapon)
                    {
                        if (!nearestWeapon.HasValue ||
                            DistanceSqr(unit.Position, lootBox.Position) <
                            DistanceSqr(unit.Position, nearestWeapon.Value.Position))
                            nearestWeapon = lootBox;
                    }
                }
                else
                {
                    if (unit.Weapon.Value.Typ == WeaponType.Pistol && lootBox.Item is Item.Weapon)
                        swapWeapon = true;
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

            bool jump = targetPos.X > unit.Position.X &&
                        game.Level.Tiles[(int) (unit.Position.X + 1)][(int) unit.Position.Y] == Tile.Wall ||
                        targetPos.X < unit.Position.X &&
                        game.Level.Tiles[(int) (unit.Position.X - 1)][(int) (unit.Position.Y)] == Tile.Wall;

            bool shoot = true;
            bool reload = false;

            double velocity = targetPos.X - unit.Position.X;

            if (nearestEnemy?.Weapon != null)
            {
                if (nearestEnemy.Value.Weapon.Value.WasShooting &&
                    nearestEnemy.Value.Weapon.Value.Typ == WeaponType.RocketLauncher)
                {
                    if (unit.Position.Y == nearestEnemy.Value.Position.Y)
                        jump = true;
                    else
                        velocity = GetVelocity(nearestEnemy.Value.Position.X, unit.Position.X, 10);
                }
            }

            if (unit.Weapon.HasValue)
            {
                if (unit.Weapon.Value.Magazine == 0)
                    reload = true;

                int minX = (int) Math.Min(targetPos.X, unit.Position.X);
                int minY = (int) Math.Min(targetPos.Y, unit.Position.Y);
                int maxX = (int) Math.Max(targetPos.X, unit.Position.X);
                int maxY = (int) Math.Max(targetPos.Y, unit.Position.Y);

                for (int i = minX; i < maxX; i++)
                    for (int j = minY; j < maxY; j++)
                        if (game.Level.Tiles[i][j] == Tile.Wall)
                            shoot = false;

                switch (unit.Weapon.Value.Typ)
                {
                    case WeaponType.AssaultRifle:
                    case WeaponType.Pistol:
                    {
                        velocity = Math.Abs(nearestEnemy.Value.Position.X - unit.Position.X) < 7
                            ? GetVelocity(targetPos.X, unit.Position.X, -15)
                            : GetVelocity(targetPos.X, unit.Position.X, 0);

                        if (Math.Abs(nearestEnemy.Value.Position.X - unit.Position.X) < 4)
                        {
                            jump = true;
                            velocity = GetVelocity(targetPos.X, unit.Position.X, 10);
                        }

                        if (DistanceSqr(unit.Position, nearestEnemy.Value.Position) > 8 && unit.Health == 100)
                            shoot = false;

                        break;
                    }

                    case WeaponType.RocketLauncher:

                        if (Math.Abs(nearestEnemy.Value.Position.X - unit.Position.X) < 7 ||
                            DistanceSqr(nearestEnemy.Value.Position, unit.Position) < 10)
                        {
                            velocity = GetVelocity(targetPos.X, unit.Position.X, -15);

                            for (int i = 0; i < 5; i++)
                            {
                                int x = (int) unit.Position.X + i * Math.Sign(velocity);
                                int y = (int) unit.Position.Y;
                                if (x < game.Level.Tiles.GetLength(0) && game.Level.Tiles[x][y] == Tile.Wall)
                                    jump = true;
                            }

                        }
                        else
                            velocity = GetVelocity(targetPos.X, unit.Position.X, 0);

                        if (DistanceSqr(unit.Position, nearestEnemy.Value.Position) > 13)
                            shoot = false;

                        break;
                }
            }
            else
            {
                velocity = GetVelocity(nearestWeapon.Value.Position.X, unit.Position.X, 10);

                if (nearestWeapon.Value.Position.X == unit.Position.X &&
                    nearestWeapon.Value.Position.Y > unit.Position.Y)
                    jump = true;
            }

            if (unit.Health != 100 && nearestWeapon != null)
            {
                velocity = GetVelocity(nearestWeapon.Value.Position.X, unit.Position.X, 15);

                if (nearestEnemy.Value.Weapon != null &&
                    (unit.Health > nearestEnemy.Value.Health &&
                     unit.Weapon.Value.Magazine > nearestEnemy.Value.Weapon.Value.Magazine))
                {
                    shoot = true;
                }
            }

            if (unit.Weapon.HasValue)
            {
                switch (unit.Weapon.Value.Typ)
                {
                    case WeaponType.RocketLauncher:
                        
                        break;

                    case WeaponType.Pistol:
                    case WeaponType.AssaultRifle:
                        
                        break;
                }
            }

            UnitAction action = new UnitAction
            {
                Velocity = velocity,
                Jump = jump,
                JumpDown = !jump,
                Aim = aim,
                Shoot = shoot,
                Reload = reload,
                SwapWeapon = swapWeapon,
                PlantMine = false
            };

            return action;
        }
    }
}