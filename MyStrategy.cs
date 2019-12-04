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

        public UnitAction GetAction(Unit unit, Game game, Debug debug)
        {
            Unit? nearestEnemy = game.Units
                .Where(otherUnit => otherUnit.PlayerId != unit.PlayerId)
                .OrderBy(otherUnit => DistanceSqr(unit.Position, otherUnit.Position))
                .Select(otherUnit => (Unit?) otherUnit)
                .FirstOrDefault();

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

                if (lootBox.Item is Item.Weapon && !unit.Weapon.HasValue)
                {
                    if (!nearestWeapon.HasValue || DistanceSqr(unit.Position, lootBox.Position) <
                        DistanceSqr(unit.Position, nearestWeapon.Value.Position))
                    {
                        if (lootBox.Item is Item.Weapon)
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

            bool shoot = true;

            double velocity = targetPos.X - unit.Position.X;
           

            if (unit.Health != 100 && nearestWeapon != null)
            {
                velocity = nearestWeapon.Value.Position.X - unit.Position.X;

                if (nearestWeapon.Value.Position.Y > unit.Position.Y)
                    jump = true;

                if (nearestEnemy.Value.Weapon != null &&
                    (unit.Health > nearestEnemy.Value.Health &&
                     unit.Weapon.Value.Magazine > nearestEnemy.Value.Weapon.Value.Magazine))
                    shoot = true;
            }

            if (nearestEnemy?.Weapon != null)
            {
                if (nearestEnemy.Value.Weapon.Value.WasShooting &&
                    nearestEnemy.Value.Weapon.Value.Typ == WeaponType.RocketLauncher)
                {
                    if (unit.Position.Y == nearestEnemy.Value.Position.Y)
                        jump = true;
                    else
                        velocity = nearestEnemy.Value.Position.X - unit.Position.X;
                }
            }

            if (unit.Weapon.HasValue)
            {
                int x, minY, maxY;
                if (unit.Position.X <= targetPos.X)
                    x = (int) (unit.Position.X - targetPos.X);
                else
                    x = (int) (targetPos.X - unit.Position.X);

                if (unit.Position.Y <= targetPos.Y)
                {
                    minY = (int) unit.Position.Y;
                    maxY = (int) targetPos.Y;
                }
                else
                {
                    minY = (int) targetPos.Y;
                    maxY = (int) unit.Position.Y;
                }

                if (unit.Weapon.Value.Typ == WeaponType.RocketLauncher)
                    for (int i = x; i > 0; i--)
                    {
                        if (game.Level.Tiles[(int)unit.Position.X + i][(maxY - minY) / 2] == Tile.Wall)
                            shoot = false;
                    }

                switch (unit.Weapon.Value.Typ)
                {
                    case WeaponType.AssaultRifle:
                    case WeaponType.RocketLauncher:
                    {
                        if (DistanceSqr(nearestEnemy.Value.Position, unit.Position) < 5)
                        {
                            velocity = (targetPos.X - unit.Position.X) * 1.2;
                            jump = true;
                        }
                        else
                            velocity = targetPos.X - unit.Position.X;

                        if (nearestEnemy.Value.Position.Y > unit.Position.Y &&
                            Math.Abs(unit.Position.X - nearestEnemy.Value.Position.X) < 2)
                        {
                            if (game.Level.Tiles[(int) (unit.Position.X + velocity + 1)][(int) unit.Position.Y] == Tile.Wall)
                                velocity -= 1;
                            if (game.Level.Tiles[(int) (unit.Position.X + velocity - 1)][(int) unit.Position.Y] == Tile.Wall)
                                velocity += 1;
                        }

                        break;
                    }
                }
            }
            else
            {
                velocity = nearestWeapon.Value.Position.X - unit.Position.X;
                if (nearestWeapon.Value.Position.X == unit.Position.X &&
                    nearestWeapon.Value.Position.Y != unit.Position.Y)
                    velocity = unit.Position.X + 10;
            }

            UnitAction action = new UnitAction
            {
                Velocity = velocity,
                Jump = jump,
                JumpDown = !jump,
                Aim = aim,
                Shoot = shoot,
                Reload = false,
                SwapWeapon = false,
                PlantMine = false
            };

            return action;
        }
    }
}