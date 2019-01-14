﻿using System;
using System.Collections.Generic;
using ImGuiNET;
using Pulsar4X.ECSLib;
using System.Numerics;
using System.Linq;

namespace Pulsar4X.SDL2UI
{
    public class WeaponTargetingControl : PulsarGuiWindow
    {
        Entity _orderingEntity;


        Dictionary<Guid, string> _weaponNames = new Dictionary<Guid, string>();
        List<Guid> _unAssignedWeapons = new List<Guid>();

        FireControlAbilityDB _shipFCDB;
        int _selectedItemIndex = -1;
        Guid _selectedFC;
        List<Guid> _selectedFCAssignedWeapons = new List<Guid>();
        Vector2 _selectableBtnSize = new Vector2(100, 18);

        Dictionary<Guid, string> _systemEntityNames = new Dictionary<Guid, string>();
        Dictionary<Guid, SensorContact> _sensorContacts = new Dictionary<Guid, SensorContact>();

        private WeaponTargetingControl(EntityState entity)
        {
            _orderingEntity = entity.Entity;
            IsActive = true;
        }

        private void OpenFire(FireControlInstanceStateDB fc, SetOpenFireControlOrder.FireModes mode)
        {
            SetOpenFireControlOrder.CreateCmd(_state.Game, _state.Faction, _orderingEntity, fc.OwningEntity.Guid, mode);
        }

        internal static WeaponTargetingControl GetInstance(EntityState entity)
        {
            if (!_state.LoadedWindows.ContainsKey(typeof(WeaponTargetingControl)))
            {
                return new WeaponTargetingControl(entity);
            }
            var instance = (WeaponTargetingControl)_state.LoadedWindows[typeof(WeaponTargetingControl)];
            instance.SetOrderEntity(entity);
            
            return instance;
        }

        internal void SetOrderEntity(EntityState entity)
        {
            _orderingEntity = entity.Entity;
            _shipFCDB = _orderingEntity.GetDataBlob<FireControlAbilityDB>();
            _weaponNames = new Dictionary<Guid, string>();
            _unAssignedWeapons = new List<Guid>();
            _systemEntityNames = new Dictionary<Guid, string>();
            _sensorContacts = new Dictionary<Guid, SensorContact>();
            for (int fcInstanceIndex = 0; fcInstanceIndex < _shipFCDB.FireControlInsances.Count; fcInstanceIndex++)
            {
                var fireControlInstance = _shipFCDB.FireControlComponents[fcInstanceIndex].GetDataBlob<ComponentInstanceInfoDB>();
            }
            for (int weaponInstanceIndex = 0; weaponInstanceIndex < _shipFCDB.WeaponInstanceStates.Count; weaponInstanceIndex++)
            {
                var weaponInstanace = _shipFCDB.WeaponComponents[weaponInstanceIndex].GetDataBlob<ComponentInstanceInfoDB>();
                string wpnname = weaponInstanace.GetName();
                _weaponNames.Add(weaponInstanace.OwningEntity.Guid, wpnname);
                //_weapons.Add(wpnname);
                if (weaponInstanace.OwningEntity.GetDataBlob<WeaponInstanceStateDB>().FireControl == null)
                    _unAssignedWeapons.Add(weaponInstanace.OwningEntity.Guid);
            }
            foreach (var item in _state.ActiveSystem.FactionSensorContacts[_state.Faction.Guid].GetAllContacts())
            {
                var entityItem = item.ActualEntity;
                string name = entityItem.GetDataBlob<NameDB>().GetName(_state.Faction);
                _systemEntityNames.Add(item.ActualEntityGuid, name);
                _sensorContacts.Add(item.ActualEntityGuid, item);
            }
            /*
            foreach (var item in _state.FactionUIState.GetEntitiesForSystem(_orderingEntity.Manager))
            {
                if (item.HasDataBlob<NameDB>() && item.HasDataBlob<PositionDB>())
                {
                    string name = item.GetDataBlob<NameDB>().GetName(_state.Faction);
                    _systemEntityNames.Add(item.Guid, name);
                    _systemEntites.Add(item.Guid, item);

                }
            }*/

        }

        internal void OnPhysicsUpdate()
        {
            var contacts = _state.ActiveSystem.FactionSensorContacts[_state.Faction.Guid].GetAllContacts();

            HashSet<Guid> contacs2 = new HashSet<Guid>(_state.ActiveSystem.FactionSensorContacts[_state.Faction.Guid].GetAllContactGuids());

            List<Guid> contactsToRemove = new List<Guid>();
            foreach (var contact in _sensorContacts)
            {
                if (!contacs2.Contains(contact.Key))
                    contactsToRemove.Add(contact.Key);
            }

            foreach (var contactGuid in contactsToRemove)
            {
                _sensorContacts.Remove(contactGuid);
            }

            foreach (var item in contacts)
            {
                if (!_sensorContacts.ContainsKey(item.ActualEntityGuid))
                {
                    var entityItem = item.ActualEntity;
                    string name = entityItem.GetDataBlob<NameDB>().GetName(_state.Faction);
                    _systemEntityNames.Add(item.ActualEntityGuid, name);
                    _sensorContacts.Add(item.ActualEntityGuid, item); 
                }
            }
        }

        internal override void Display()
        {
            if (IsActive)
            {
                Vector2 size = new Vector2(200, 100);
                Vector2 pos = new Vector2(_state.MainWinSize.X / 2 - size.X / 2, _state.MainWinSize.Y / 2 - size.Y / 2);

                ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowPos(pos, ImGuiCond.FirstUseEver);

                if (ImGui.Begin("Weapon Targeting", ref IsActive, _flags))
                {
                    
                    int selectable = 0;
                    bool selected = false;
                    ImGui.BeginGroup();
                    foreach (var fc in _shipFCDB.FireControlInsances)
                    {
                        

                        if (ImGui.Selectable("FC " + selectable, selected, ImGuiSelectableFlags.None, _selectableBtnSize))
                        {
                            _selectedItemIndex = selectable;
                            _selectedFC = fc.OwningEntity.Guid;
                            _selectedFCAssignedWeapons = new List<Guid>();
                            foreach (var item in fc.AssignedWeapons)
                            {
                                _selectedFCAssignedWeapons.Add(item.Guid);
                            }

                        }
                        /*
                        ImGui.Text("Assigned Weapons: ");
                        foreach (var weapon in fc.AssignedWeapons)
                        {
                            bool isEnabled = weapon.GetDataBlob<ComponentInstanceInfoDB>().IsEnabled;
                            if (ImGui.Checkbox(weaponNames[weapon.Guid], ref isEnabled))
                            {
                                //give an enable/disable order. 
                            }
                        }*/
                        ImGui.Text("Target: ");
                        ImGui.SameLine();
                        if (fc.Target == null || !fc.Target.IsValid)
                            ImGui.Text("No Target");
                        else
                            ImGui.Text(fc.Target.GetDataBlob<NameDB>().GetName(_state.Faction));
                        selectable++;

                        if (fc.IsEngaging)
                        {
                            if (ImGui.Button("Cease Fire"))
                                OpenFire(fc, SetOpenFireControlOrder.FireModes.CeaseFire);
                        }
                        else
                        {
                            if (ImGui.Button("Open Fire"))
                                OpenFire(fc, SetOpenFireControlOrder.FireModes.OpenFire);
                        }
                    }
                    ImGui.EndGroup();
                    if (_selectedItemIndex > -1)
                    {

                        ImGui.SameLine();
                        ImGui.BeginGroup();
                        {
                            ImGui.BeginGroup();
                            //ImGui.BeginChild("AssignedWeapons", true);

                            ImGui.Text("Assigned Weapons");
                            foreach (var wpn in _selectedFCAssignedWeapons.ToArray())
                            {
                                if (ImGui.Button(_weaponNames[wpn]))
                                {
                                    
                                    _unAssignedWeapons.Add(wpn);
                                    _selectedFCAssignedWeapons.Remove(wpn);
                                    SetWeaponsFireControlOrder.CreateCommand(_state.Game, _state.CurrentSystemDateTime, _state.Faction.Guid, _orderingEntity.Guid, _selectedFC, _selectedFCAssignedWeapons);
                                }
                            }
                            ImGui.EndGroup();
                            ImGui.BeginGroup();
                            //ImGui.EndChild();
                            //ImGui.BeginChild("Un Assigned Weapons", true);

                            ImGui.Text("Un Assigned Weapons");
                            foreach (var wpn in _unAssignedWeapons.ToArray())
                            {
                                if (ImGui.Button(_weaponNames[wpn]))
                                {
                                    _selectedFCAssignedWeapons.Add(wpn);
                                    _unAssignedWeapons.Remove(wpn);
                                    SetWeaponsFireControlOrder.CreateCommand(_state.Game, _state.CurrentSystemDateTime, _state.Faction.Guid, _orderingEntity.Guid, _selectedFC, _selectedFCAssignedWeapons);
                                }
                            }
                            ImGui.EndGroup();
                            //ImGui.EndChild();

                        }
                        ImGui.EndGroup();
                        ImGui.SameLine();
                        ImGui.BeginGroup();
                        {
                            ImGui.Text("Set Target");
                            foreach (var item in _systemEntityNames)
                            {
                                if (ImGui.SmallButton(item.Value))
                                {
                                    SetTargetFireControlOrder.CreateCommand(_state.Game, _state.CurrentSystemDateTime, _state.Faction.Guid, _orderingEntity.Guid, _selectedFC, item.Key);
                                }
                            }

                        }
                        ImGui.EndGroup();
                        ImGui.SameLine();
                        ImGui.BeginGroup();
                        {
                            ImGui.Text("Range in AU");
                            foreach (var item in _sensorContacts)
                            {
                                var targetEntity = _sensorContacts[item.Key];
                                double distance = _orderingEntity.GetDataBlob<PositionDB>().GetDistanceTo(targetEntity.Position);
                                ImGui.Text(distance.ToString());
                            }

                        }
                        ImGui.EndGroup();
                    }
                }
                ImGui.End();
            }
        }
    }


}
