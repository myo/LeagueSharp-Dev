#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Targets.cs is part of SFXChallenger.

 SFXChallenger is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXChallenger is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXChallenger. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using SFXChallenger.Library;
using SFXChallenger.Library.Logger;

#endregion

namespace SFXChallenger.SFXTargetSelector
{
    public static class Targets
    {
        static Targets()
        {
            try
            {
                Items = new HashSet<Item>();
                foreach (var enemy in GameObjects.EnemyHeroes)
                {
                    Items.Add(new Item(enemy));
                }
                Core.OnPreUpdate += OnCorePreUpdate;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static HashSet<Item> Items { get; private set; }

        private static void OnCorePreUpdate(EventArgs args)
        {
            try
            {
                foreach (var item in Items.Where(item => item.Visible != !item.Hero.IsVisible))
                {
                    item.Visible = item.Hero.IsVisible;
                    item.LastVisibleChange = Game.Time;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public class Item
        {
            public Item(Obj_AI_Hero hero)
            {
                Hero = hero;
                LastVisibleChange = Game.Time;
            }

            public Obj_AI_Hero Hero { get; private set; }
            public float Weight { get; set; }
            public float SimulatedWeight { get; set; }
            public float LastVisibleChange { get; set; }
            public bool Visible { get; set; }
        }
    }
}