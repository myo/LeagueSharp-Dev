﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Cassiopeia.cs is part of SFXChallenger.

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

namespace SFXChallenger.Champions
{
    #region

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Abstracts;
    using Enumerations;
    using LeagueSharp;
    using LeagueSharp.Common;
    using Managers;
    using SFXLibrary.Extensions.NET;
    using SFXLibrary.Extensions.SharpDX;
    using SFXLibrary.Logger;
    using SharpDX;
    using Wrappers;
    using Color = System.Drawing.Color;
    using Orbwalking = Wrappers.Orbwalking;
    using TargetSelector = Wrappers.TargetSelector;

    #endregion

    internal class Cassiopeia : Champion
    {
        private float _lastPoisonClearDelay;
        private Vector2 _lastPoisonClearPosition;
        private float _lastQPoisonDelay;
        private Obj_AI_Base _lastQPoisonT;
        private List<Obj_AI_Hero> _targets = new List<Obj_AI_Hero>();

        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override void OnLoad()
        {
            Core.OnPreUpdate += OnCorePreUpdate;
            Core.OnPostUpdate += OnCorePostUpdate;
            Orbwalking.BeforeAttack += OnOrbwalkingBeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget += OnInterruptableTarget;
        }

        protected override void OnUnload()
        {
            Core.OnPreUpdate -= OnCorePreUpdate;
            Core.OnPostUpdate -= OnCorePostUpdate;
            Orbwalking.BeforeAttack -= OnOrbwalkingBeforeAttack;
            AntiGapcloser.OnEnemyGapcloser -= OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget -= OnInterruptableTarget;
        }

        protected override void AddToMenu()
        {
            var drawingMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Drawing"), Menu.Name + ".drawing"));
            drawingMenu.AddItem(
                new MenuItem(drawingMenu.Name + ".circle-thickness", Global.Lang.Get("G_CircleThickness")).SetValue(new Slider(2, 0, 10)));
            drawingMenu.AddItem(new MenuItem(drawingMenu.Name + ".q", "Q").SetValue(new Circle(false, Color.White)));
            drawingMenu.AddItem(new MenuItem(drawingMenu.Name + ".w", "W").SetValue(new Circle(false, Color.White)));
            drawingMenu.AddItem(new MenuItem(drawingMenu.Name + ".e", "E").SetValue(new Circle(false, Color.White)));
            drawingMenu.AddItem(new MenuItem(drawingMenu.Name + ".r", "R").SetValue(new Circle(false, Color.White)));

            var comboMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("C_Combo"), Menu.Name + ".combo"));
            ManaManager.AddToMenu(comboMenu, "combo", 0);
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", Global.Lang.Get("C_UseQ")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".w", Global.Lang.Get("C_UseW")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", Global.Lang.Get("C_UseE")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".r", Global.Lang.Get("C_UseR")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".r-min-facing", Global.Lang.Get("Cassio_RMinFacing")).SetValue(new Slider(1, 1, 5)));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".r-min", Global.Lang.Get("Cassio_RMin")).SetValue(new Slider(3, 1, 5)));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".r-1v1", Global.Lang.Get("Cassio_R1v1")).SetValue(true));

            var harassMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("C_Harass"), Menu.Name + ".harass"));
            ManaManager.AddToMenu(harassMenu, "harass");
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", Global.Lang.Get("C_UseQ")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".w", Global.Lang.Get("C_UseW")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e", Global.Lang.Get("C_UseE")).SetValue(true));

            var laneclearMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("C_LaneClear"), Menu.Name + ".lane-clear"));
            ManaManager.AddToMenu(laneclearMenu, "lane-clear");
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", Global.Lang.Get("C_UseQ")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".w", Global.Lang.Get("C_UseW")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e", Global.Lang.Get("C_UseE")).SetValue(true));

            var autoMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("C_Auto"), Menu.Name + ".auto"));
            autoMenu.AddItem(new MenuItem(autoMenu.Name + ".r-min-facing", Global.Lang.Get("Cassio_RMinFacing")).SetValue(new Slider(3, 1, 5)));
            autoMenu.AddItem(new MenuItem(autoMenu.Name + ".r-min", Global.Lang.Get("Cassio_RMin")).SetValue(new Slider(4, 1, 5)));
            autoMenu.AddItem(new MenuItem(autoMenu.Name + ".enabled", Global.Lang.Get("G_Enabled")).SetValue(true));

            var flashMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("C_Flash"), Menu.Name + ".flash"));
            flashMenu.AddItem(new MenuItem(flashMenu.Name + ".r-min-facing", Global.Lang.Get("Cassio_RMinFacing")).SetValue(new Slider(3, 1, 5)));
            flashMenu.AddItem(new MenuItem(flashMenu.Name + ".r-min-killable", Global.Lang.Get("Cassio_RMinKillable")).SetValue(new Slider(2, 1, 5)));
            flashMenu.AddItem(new MenuItem(flashMenu.Name + ".r-min", Global.Lang.Get("Cassio_RMin")).SetValue(new Slider(4, 1, 5)));
            flashMenu.AddItem(
                new MenuItem(flashMenu.Name + ".r-hotkey", "R " + Global.Lang.Get("G_Hotkey")).SetValue(new KeyBind('U', KeyBindType.Press)));

            var fleeMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("C_Flee"), Menu.Name + ".flee"));
            ManaManager.AddToMenu(fleeMenu, "flee", 15);
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".w", Global.Lang.Get("C_UseW")).SetValue(true));

            var miscMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Miscellaneous"), Menu.Name + ".miscellaneous"));
            ManaManager.AddToMenu(miscMenu, "misc", 15);
            miscMenu.AddItem(new MenuItem(miscMenu.Name + ".r-gapcloser", "R " + Global.Lang.Get("G_Gapcloser")).SetValue(false));
            miscMenu.AddItem(new MenuItem(miscMenu.Name + ".r-important", "R " + Global.Lang.Get("G_ImportantSpell")).SetValue(false));
            miscMenu.AddItem(new MenuItem(miscMenu.Name + ".e-lasthit", "E " + Global.Lang.Get("G_Lasthit")).SetValue(false));
            miscMenu.AddItem(new MenuItem(miscMenu.Name + ".w-stunned", "W " + Global.Lang.Get("C_Stunned")).SetValue(false));
            miscMenu.AddItem(new MenuItem(miscMenu.Name + ".e-killsteal", "E " + Global.Lang.Get("C_Killsteal")).SetValue(false));
            miscMenu.AddItem(new MenuItem(miscMenu.Name + ".r-killsteal", "R " + Global.Lang.Get("C_Killsteal")).SetValue(new Slider(3, 1, 5)));
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 850f);
            Q.SetSkillshot(0.6f, 40f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            W = new Spell(SpellSlot.W, 850f);
            W.SetSkillshot(0.5f, 90f, 2500f, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 700f);
            E.SetTargetted(0.2f, 1900f);

            R = new Spell(SpellSlot.R, 800f);
            R.SetSkillshot(0.7f, (float) (80*Math.PI/180), float.MaxValue, false, SkillshotType.SkillshotCone);
        }

        private void OnCorePreUpdate(EventArgs args)
        {
            if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LaneClear && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LastHit &&
                Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None)
            {
                _targets = TargetSelector.GetTargets(850f, LeagueSharp.Common.TargetSelector.DamageType.Magical).Select(t => t.Hero).ToList();
            }
            if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit && Menu.Item(Menu.Name + ".miscellaneous.e-lasthit").GetValue<bool>() &&
                 ManaManager.Check("misc")) && E.IsReady())
            {
                var m =
                    MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.NotAlly)
                        .FirstOrDefault(e => e.Health < E.GetDamage(e) - 5);
                if (m != null)
                {
                    E.Cast(m, true);
                }
            }
        }

        private void OnCorePostUpdate(EventArgs args)
        {
            if (Menu.Item(Menu.Name + ".auto.enabled").GetValue<bool>())
            {
                RLogic(Menu.Item(Menu.Name + ".auto.r-min").GetValue<Slider>().Value,
                    Menu.Item(Menu.Name + ".auto.r-min-facing").GetValue<Slider>().Value);
            }
            if (Menu.Item(Menu.Name + ".flash.r-hotkey").GetValue<KeyBind>().Active && R.IsReady() && SummonerManager.Flash.IsReady())
            {
                Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                var targets =
                    TargetSelector.GetTargets((R.Range*0.9f) + SummonerManager.Flash.Range)
                        .Where(
                            t =>
                                t.Hero != null &&
                                Prediction.GetPrediction(t.Hero, R.Delay + 0.3f).UnitPosition.Distance(Player.Position) > R.Range*1.1);
                foreach (var target in targets)
                {
                    var flashPos = Player.Position.Extend(target.Hero.Position, SummonerManager.Flash.Range);
                    var minFacing = Menu.Item(Menu.Name + ".flash.r-min-facing").GetValue<Slider>().Value;
                    var pred =
                        Prediction.GetPrediction(new PredictionInput
                        {
                            Aoe = true,
                            Collision = false,
                            CollisionObjects = new[] {CollisionableObjects.YasuoWall},
                            From = flashPos,
                            RangeCheckFrom = flashPos,
                            Delay = R.Delay + 0.3f,
                            Range = R.Range*0.9f,
                            Speed = R.Speed,
                            Radius = R.Width,
                            Type = SkillshotType.SkillshotCone,
                            Unit = target.Hero
                        });
                    if (pred.Hitchance >= HitChance.Medium)
                    {
                        var rHits = HeroManager.Enemies.Where(x => R.WillHit(flashPos, pred.CastPosition)).ToList();
                        var inRange = rHits.Count;
                        var isFacing = rHits.Count(enemy => IsFacing(enemy, Player));
                        var killable = rHits.Count(enemy => enemy.Health < R.GetDamage(enemy) - 20);
                        if (isFacing >= minFacing || inRange >= Menu.Item(Menu.Name + ".flash.r-min").GetValue<Slider>().Value ||
                            killable >= Menu.Item(Menu.Name + ".flash.r-min-killable").GetValue<Slider>().Value)
                        {
                            if (minFacing == 1 && (!target.Hero.CanMove || target.Hero.IsImmovable || target.Hero.IsWindingUp) || minFacing > 1)
                            {
                                var pos = Player.Position.Extend(pred.CastPosition, -(Player.Position.Distance(pred.CastPosition)*2));
                                R.Cast(pos, true);
                                Utility.DelayAction.Add(300, () => SummonerManager.Flash.Cast(pred.CastPosition));
                            }
                        }
                    }
                }
            }
            if (ManaManager.Check("misc"))
            {
                if (Menu.Item(Menu.Name + ".miscellaneous.w-stunned").GetValue<bool>() && W.IsReady())
                {
                    var target =
                        _targets.Where(t => t.Distance(Player) <= W.Range)
                            .FirstOrDefault(t => t.IsValidTarget(W.Range) && t.IsStunned || t.IsCharmed || t.IsRooted);
                    if (target != null)
                    {
                        Casting.BasicSkillShot(target, W, HitchanceManager.Get("w"));
                    }
                }
            }
        }

        private void OnOrbwalkingBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            var t = args.Target as Obj_AI_Hero;
            if (t != null && (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Harass))
            {
                args.Process = false;
            }
            if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit && Menu.Item(Menu.Name + ".miscellaneous.e-lasthit").GetValue<bool>() &&
                 ManaManager.Check("misc")))
            {
                var m = args.Target as Obj_AI_Minion;
                if (m != null && E.CanCast(m))
                {
                    if (Player.GetSpell(E.Slot).ManaCost < Player.Mana)
                    {
                        args.Process = false;
                    }
                }
            }
        }

        private bool IsFacing(Obj_AI_Base source, Obj_AI_Base target)
        {
            if (source == null || target == null)
                return false;
            return source.Direction.To2D().Perpendicular().AngleBetween((target.Position - source.Position).To2D()) < 80.0;
        }

        private void OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (sender.IsAlly || args.DangerLevel != Interrupter2.DangerLevel.High || args.EndTime <= Game.Time + R.Delay ||
                !ManaManager.Check("misc"))
                return;

            if (Menu.Item(Menu.Name + ".miscellaneous.r-important").GetValue<bool>())
            {
                Casting.BasicSkillShot(sender, R, HitchanceManager.Get("r"));
            }
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Menu.Item(Menu.Name + ".miscellaneous.r-gapcloser").GetValue<bool>() || !ManaManager.Check("misc"))
            {
                Casting.BasicSkillShot(gapcloser.Sender, R, HitchanceManager.Get("r"));
            }
        }

        protected override void Combo()
        {
            var q = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady();
            var w = Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() && W.IsReady();
            var e = Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady();
            var r = Menu.Item(Menu.Name + ".combo.r").GetValue<bool>() && R.IsReady();

            if (q)
            {
                QLogic();
            }
            if (w)
            {
                WLogic();
            }
            if (e)
            {
                ELogic();
            }
            if (r)
            {
                RLogic(Menu.Item(Menu.Name + ".combo.r-min").GetValue<Slider>().Value,
                    Menu.Item(Menu.Name + ".combo.r-min-facing").GetValue<Slider>().Value);
            }

            var t2 = _targets.FirstOrDefault(t => t.Distance(Player) <= R.Range);
            if (t2 != null)
            {
                var cDmg = CalcComboDamage(t2, q, w, e, r);
                if (r)
                {
                    if (Menu.Item(Menu.Name + ".combo.r-1v1").GetValue<bool>() && cDmg >= t2.Health - 20)
                    {
                        if (HeroManager.Enemies.Count(em => !em.IsDead && em.IsVisible && em.Distance(Player) < 3000) == 1)
                        {
                            Casting.BasicSkillShot(t2, R, HitchanceManager.Get("r"));
                            var pred = R.GetPrediction(t2, true);
                            if (pred.Hitchance >= HitchanceManager.Get("r"))
                            {
                                R.Cast(pred.CastPosition, true);
                            }
                        }
                    }
                }
                if (cDmg*1.5 > t2.Health)
                {
                    ItemManager.UseComboItems(t2);
                    SummonerManager.UseComboSummoners(t2);
                }
            }
        }

        private bool IsNearTurret(Obj_AI_Base target)
        {
            return ObjectManager.Get<Obj_AI_Turret>().Any(turret => turret.IsValidTarget(1300f, true, target.Position));
        }

        private float CalcComboDamage(Obj_AI_Base target, bool q, bool w, bool e, bool r)
        {
            var manaCost = w && W.IsReady() ? Player.GetSpell(W.Slot).ManaCost : (q ? Player.GetSpell(Q.Slot).ManaCost : 0);
            var damage = w && W.IsReady() ? W.GetDamage(target) : (q ? Q.GetDamage(target) : 0);

            if (e)
            {
                var eMana = Player.GetSpell(E.Slot).ManaCost;
                var eDamage = E.GetDamage(target);
                var count = IsNearTurret(target) || !R.CanCast(target) ? 3 : 6;
                for (var i = 0; i < count; i++)
                {
                    if (manaCost + eMana > Player.Mana)
                    {
                        break;
                    }
                    manaCost += eMana;
                    damage += eDamage;
                }
            }
            if (r)
            {
                if (manaCost + Player.GetSpell(R.Slot).ManaCost - 10 > Player.Mana)
                {
                    return damage;
                }
                return damage + (R.IsReady() ? R.GetDamage(target) : 0);
            }
            return damage;
        }

        private void RLogic(int min, int minFacing)
        {
            var target = _targets.FirstOrDefault(t => t.Distance(Player) <= R.Range);
            if (target != null)
            {
                var pred = R.GetPrediction(target, true);
                if (pred.Hitchance >= HitchanceManager.Get("r"))
                {
                    var rHits = HeroManager.Enemies.Where(x => R.WillHit(x.Position, pred.CastPosition)).ToList();
                    var inRange = rHits.Count(enemy => enemy.Position.Distance(Player.Position) < R.Range);
                    var isFacing = rHits.Count(enemy => IsFacing(enemy, Player));
                    if (isFacing >= minFacing || inRange >= min)
                    {
                        if (minFacing == 1 && (!target.CanMove || target.IsImmovable || target.IsWindingUp) || minFacing > 1)
                        {
                            R.Cast(pred.CastPosition, true);
                        }
                    }
                }
            }
        }

        private void QLogic()
        {
            var ts = _targets.FirstOrDefault(t => t.Distance(Player) <= Q.Range && GetPoisonBuffEndTime(t) < Q.Delay*1.2f);
            if (ts != null)
            {
                _lastQPoisonDelay = Game.Time + Q.Delay;
                _lastQPoisonT = ts;
                Casting.BasicSkillShot(ts, Q, HitchanceManager.Get("q"));
            }
        }

        private void WLogic()
        {
            var tsAll = _targets.Where(t => t.Distance(Player) <= W.Range).ToList();
            foreach (var ts in tsAll)
            {
                if ((!IsFacing(ts, Player) && ts.Position.Distance(Player.Position) > W.Range*0.7f) || tsAll.Count() == 1 ||
                    (_lastQPoisonDelay < Game.Time && GetPoisonBuffEndTime(ts) < W.Delay*1.2) || _lastQPoisonT.NetworkId != ts.NetworkId)
                {
                    Casting.BasicSkillShot(ts, W, HitchanceManager.Get("w"));
                    return;
                }
            }
        }

        private void ELogic()
        {
            var ts = _targets.FirstOrDefault(t => t.Distance(Player) <= E.Range && GetPoisonBuffEndTime(t) > GetEDelay(t));
            if (ts != null)
            {
                E.Cast(ts, true);
            }
        }

        protected override void Harass()
        {
            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>())
            {
                QLogic();
            }
            if (Menu.Item(Menu.Name + ".harass.w").GetValue<bool>())
            {
                WLogic();
            }
            if (Menu.Item(Menu.Name + ".harass.e").GetValue<bool>())
            {
                ELogic();
            }
        }

        private float GetPoisonBuffEndTime(Obj_AI_Base target)
        {
            try
            {
                var buffEndTime = target == null || !target.IsValid
                    ? 0
                    : (target.Buffs.Where(b => b.Type == BuffType.Poison).Select(b => b.EndTime).DefaultIfEmpty().Max(end => end - Game.Time));
                return buffEndTime;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        private float GetEDelay(Obj_AI_Base target)
        {
            return (E.Delay + (E.Delay > 0 ? (ObjectManager.Player.ServerPosition.Distance(target.ServerPosition)/E.Speed) : 0)) + 0.1f;
        }

        protected override void LaneClear()
        {
            var q = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>();
            var w = Menu.Item(Menu.Name + ".lane-clear.w").GetValue<bool>();
            if (q || w)
            {
                var minions =
                    MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range + Q.Width)
                        .Where(e => GetPoisonBuffEndTime(e) < Q.Delay*1.1)
                        .OrderByDescending(m => m.BaseSkinName.Contains("MinionSiege", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                if (minions.Any())
                {
                    if (q)
                    {
                        var prediction = Q.GetCircularFarmLocation(minions, Q.Width + 30);
                        if (prediction.MinionsHit > 1 && _lastPoisonClearDelay < Game.Time)
                        {
                            _lastPoisonClearDelay = Game.Time + Q.Delay;
                            _lastPoisonClearPosition = prediction.Position;
                            Q.Cast(prediction.Position);
                        }
                    }
                    if (w)
                    {
                        var prediction = W.GetCircularFarmLocation(minions, W.Width + 40);
                        if (prediction.MinionsHit > 2 && _lastPoisonClearDelay < Game.Time)
                        {
                            _lastPoisonClearDelay = Game.Time + W.Delay;
                            _lastPoisonClearPosition = prediction.Position;
                            W.Cast(prediction.Position);
                        }
                    }
                }
                else
                {
                    var creeps =
                        MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range + Q.Width, MinionTypes.All, MinionTeam.Neutral)
                            .Where(e => GetPoisonBuffEndTime(e) < Q.Delay*1.1 && !e.IsMoving)
                            .ToList();
                    if (creeps.Any())
                    {
                        if (q)
                        {
                            var pred = Q.GetCircularFarmLocation(creeps, Q.Width + 30);
                            if (_lastPoisonClearDelay < Game.Time)
                            {
                                _lastPoisonClearDelay = Game.Time + Q.Delay;
                                _lastPoisonClearPosition = pred.Position;
                                Q.Cast(pred.Position);
                            }
                        }
                        var prediction = W.GetCircularFarmLocation(creeps, W.Width + 40);
                        if (w && prediction.MinionsHit > 1 && _lastPoisonClearDelay < Game.Time)
                        {
                            _lastPoisonClearDelay = Game.Time + W.Delay;
                            _lastPoisonClearPosition = prediction.Position;
                            W.Cast(prediction.Position);
                        }
                    }
                }
            }
            if (Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>())
            {
                var minion =
                    MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.NotAlly)
                        .Where(
                            e =>
                                GetPoisonBuffEndTime(e) > GetEDelay(e) &&
                                (e.Team == GameObjectTeam.Neutral || (e.Health > E.GetDamage(e)*2 || e.Health < E.GetDamage(e) - 5)))
                        .OrderByDescending(m => m.BaseSkinName.Contains("MinionSiege", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                if (minion.Any())
                {
                    Casting.BasicTargetSkill(minion.First(), E, HitchanceManager.Get("e"), true);
                }
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.w").GetValue<bool>() && E.IsReady())
            {
                var near = HeroManager.Enemies.OrderBy(e => e.Distance(Player.Position)).FirstOrDefault();
                if (near != null)
                {
                    var pred = W.GetPrediction(near, true);
                    if (pred.Hitchance >= HitchanceManager.Get("w"))
                    {
                        W.Cast(pred.CastPosition);
                    }
                }
            }
            ItemManager.UseFleeItems();
        }

        protected override void Killsteal()
        {
            if (Menu.Item(Menu.Name + ".miscellaneous.e-killsteal").GetValue<bool>() && E.IsReady())
            {
                var enemy = HeroManager.Enemies.FirstOrDefault(e => e.IsValidTarget(E.Range) && e.Health < E.GetDamage(e) - 10);
                if (enemy != null)
                {
                    E.Cast(enemy, true);
                }
            }
            if (R.IsReady())
            {
                var target = _targets.FirstOrDefault(t => t.Distance(Player) <= R.Range);
                if (target != null)
                {
                    var pred = R.GetPrediction(target, true);
                    if (pred.Hitchance >= HitchanceManager.Get("r"))
                    {
                        var rHits = HeroManager.Enemies.Where(x => R.WillHit(x.Position, pred.CastPosition)).ToList();
                        if (rHits.Count >= Menu.Item(Menu.Name + ".miscellaneous.r-killsteal").GetValue<Slider>().Value &&
                            rHits.Any(r => R.GetDamage(r) - 10 > r.Health))
                        {
                            R.Cast(pred.CastPosition, true);
                        }
                    }
                }
            }
            KillstealManager.Killsteal();
        }

        protected override void OnDraw()
        {
            var q = Menu.Item(Menu.Name + ".drawing.q").GetValue<Circle>();
            var w = Menu.Item(Menu.Name + ".drawing.w").GetValue<Circle>();
            var e = Menu.Item(Menu.Name + ".drawing.e").GetValue<Circle>();
            var r = Menu.Item(Menu.Name + ".drawing.r").GetValue<Circle>();
            var circleThickness = Menu.Item(Menu.Name + ".drawing.circle-thickness").GetValue<Slider>().Value;

            if (q.Active && Player.Position.IsOnScreen(Q.Range))
            {
                Render.Circle.DrawCircle(Player.Position, Q.Range, q.Color, circleThickness);
            }
            if (w.Active && Player.Position.IsOnScreen(W.Range))
            {
                Render.Circle.DrawCircle(Player.Position, W.Range, w.Color, circleThickness);
            }
            if (e.Active && Player.Position.IsOnScreen(E.Range))
            {
                Render.Circle.DrawCircle(Player.Position, E.Range, e.Color, circleThickness);
            }
            if (r.Active && Player.Position.IsOnScreen(R.Range))
            {
                Render.Circle.DrawCircle(Player.Position, R.Range, r.Color, circleThickness);
            }
        }
    }
}