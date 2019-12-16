using System;
using System.Collections.Generic;
using System.Drawing;
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

        static List<Point> GetLine(Vec2Double start, Vec2Double finish)
        {
            int startX = (int) start.X;
            int startY = (int) start.Y;

            int finishX = (int) finish.X;
            int finishY = (int) finish.Y;

            int dx = Math.Abs(finishX - startX), sx = startX < finishX ? 1 : -1;
            int dy = Math.Abs(finishY - startY), sy = startY < finishY ? 1 : -1;
            int err = (dx > dy ? dx : -dy) / 2;

            List<Point> lineList = new List<Point>();

            for (;;)
            {

                if (startX == finishX && startY == finishY)
                    break;

                lineList.Add(new Point(startX, startY));

                var e2 = err;

                if (e2 > -dx)
                {
                    err -= dy;
                    startX += sx;
                }

                if (e2 < dy)
                {
                    err += dx;
                    startY += sy;
                }
            }

            return lineList;
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
                    if (unit.Weapon.Value.Typ == WeaponType.AssaultRifle &&
                        lootBox.Item is Item.Weapon)
                        swapWeapon = true;

                    if (lootBox.Item is Item.HealthPack && unit.Health != 100)
                    {
                        if (!nearestWeapon.HasValue || DistanceSqr(unit.Position, lootBox.Position) <
                            DistanceSqr(unit.Position, nearestWeapon.Value.Position))
                        {
                            nearestWeapon = lootBox;
                        }
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
                targetPos.Y = nearestEnemy.Value.Position.Y + nearestEnemy.Value.Size.Y / 2;
            }

            debug.Draw(new CustomData.Log("Target pos: " + targetPos));

            Vec2Double aim = new Vec2Double(0, 0);
            if (nearestEnemy.HasValue)
            {
                aim = new Vec2Double(nearestEnemy.Value.Position.X - unit.Position.X,
                    nearestEnemy.Value.Position.Y - unit.Position.Y);
            }

            bool jump = game.Level.Tiles[(int) (unit.Position.X + 1)][(int) unit.Position.Y] == Tile.Wall ||
                        game.Level.Tiles[(int) (unit.Position.X - 1)][(int) (unit.Position.Y)] == Tile.Wall ||
                        nearestEnemy.Value.Position.Y > unit.Position.Y;


            bool shoot = true;
            bool reload = false;

            double velocity = GetVelocity(targetPos.X, unit.Position.X, 1);
            List<Point> bulletTrajectory = GetLine(unit.Position, targetPos);

            if (nearestEnemy?.Weapon != null)
            {
                if (nearestEnemy.Value.Weapon.Value.WasShooting)
                {
                    if (unit.Position.Y == nearestEnemy.Value.Position.Y)
                        jump = true;

                    if (unit.Position.Y > nearestEnemy.Value.Position.Y)
                        velocity = GetVelocity(nearestEnemy.Value.Position.X, unit.Position.X, -5);

                    if (unit.Position.Y < nearestEnemy.Value.Position.Y)
                        velocity = GetVelocity(nearestEnemy.Value.Position.X, unit.Position.X, 5);
                }
            }

            if (unit.Weapon.HasValue)
            {
                if (unit.Weapon.Value.Magazine == 0)
                    reload = true;

                foreach (Point point in bulletTrajectory)
                {
                    if (game.Level.Tiles[point.X][point.Y] == Tile.Wall)
                        shoot = false;
                }

                switch (unit.Weapon.Value.Typ)
                {
                    case WeaponType.AssaultRifle:
                    case WeaponType.Pistol:
                    {
                        velocity = Math.Abs(nearestEnemy.Value.Position.X - unit.Position.X) < 5
                            ? GetVelocity(targetPos.X, unit.Position.X, -15)
                            : GetVelocity(targetPos.X, unit.Position.X, 0);

                        if (Math.Abs(nearestEnemy.Value.Position.X - unit.Position.X) < 4)
                        {
                            jump = true;
                            velocity = GetVelocity(targetPos.X, unit.Position.X, 7);
                        }

                        if (nearestEnemy.Value.Weapon.HasValue &&
                            nearestEnemy.Value.Weapon.Value.Typ == WeaponType.AssaultRifle &&
                            unit.Weapon.Value.Typ == WeaponType.Pistol)
                            velocity  = GetVelocity(targetPos.X, unit.Position.X, 0);

                        if (unit.Weapon.Value.Spread == unit.Weapon.Value.Parameters.MaxSpread &&
                            DistanceSqr(unit.Position, targetPos) > 1)
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
                                x = x > 0 ? x : 0;
                                int y = (int) unit.Position.Y;
                                if (x < game.Level.Tiles.GetLength(0) && game.Level.Tiles[x][y] == Tile.Wall)
                                    jump = true;
                            }

                        }
                        else
                            velocity = GetVelocity(targetPos.X, unit.Position.X, 0);

                        break;
                }
            }
            else
            {
                velocity = nearestWeapon.Value.Position.X != unit.Position.X
                    ? GetVelocity(nearestWeapon.Value.Position.X, unit.Position.X, 10)
                    : unit.Position.X;

                if (nearestWeapon.Value.Position.Y >= unit.Position.Y &&
                    Math.Sign(nearestWeapon.Value.Position.X - unit.Position.X) < 0 &&
                    nearestWeapon.Value.Position.X != unit.Position.X)
                {
                    jump = true;
                    velocity = nearestWeapon.Value.Position.X - unit.Position.X;
                }
                else
                {
                    jump = false;
                }
            }

            if (unit.Health < game.Properties.UnitMaxHealth * 0.75 && nearestWeapon != null)
            {
                if (unit.Position.X != nearestWeapon.Value.Position.X)
                    velocity = GetVelocity(nearestWeapon.Value.Position.X, unit.Position.X, 10);
                else
                    jump = true;

                if (nearestEnemy.Value.Weapon != null &&
                    (unit.Health > nearestEnemy.Value.Health &&
                     unit.Weapon.Value.Magazine > nearestEnemy.Value.Weapon.Value.Magazine))
                {
                    shoot = true;
                }
            }

            if (nearestWeapon.HasValue &&
                unit.Position.X == nearestWeapon.Value.Position.X &&
                unit.Position.Y != nearestWeapon.Value.Position.Y)
            {
                var wayToLootBox = GetLine(unit.Position, nearestWeapon.Value.Position);

                if(unit.Position.Y > nearestWeapon.Value.Position.Y)
                    foreach (Point point in wayToLootBox)
                    {
                        if (game.Level.Tiles[point.X][point.Y] == Tile.Wall)
                            jump = false;
                    }

                if (unit.Position.Y < nearestWeapon.Value.Position.Y)
                    foreach (Point point in wayToLootBox)
                    {
                        if (game.Level.Tiles[point.X][point.Y] == Tile.Wall)
                            jump = true;
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