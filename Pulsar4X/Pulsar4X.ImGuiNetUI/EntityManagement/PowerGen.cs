using System;
using System.Numerics;
using ImGuiNET;
using ImGuiSDL2CS;
using Pulsar4X.ECSLib;
using Pulsar4X.SDL2UI;

namespace Pulsar4X.ImGuiNetUI.EntityManagement
{
    public class PowerGen : PulsarGuiWindow
    {
        private EntityState _entityState;
        Vector2 _plotSize = new Vector2(512, 64);
        private EnergyGenAbilityDB _energyGenDB;
        
        
        internal static PowerGen GetInstance()
        {
            PowerGen instance;
            if (!_state.LoadedWindows.ContainsKey(typeof(PowerGen)))
            {
                instance = new PowerGen(_state.LastClickedEntity);
            }
            else
                instance = (PowerGen)_state.LoadedWindows[typeof(PowerGen)];
            if(instance._entityState != _state.LastClickedEntity)
                instance.SetEntity(_state.LastClickedEntity);
            //instance._sysState = _state.StarSystemStates[_state.SelectedSystem.Guid];
            _state.SelectedSystem.ManagerSubpulses.SystemDateChangedEvent += instance.ManagerSubpulses_SystemDateChangedEvent;


            return instance;
        }

        private PowerGen(EntityState entity)
        {
            _entityState = entity;
        }

        public void SetEntity(EntityState entityState)
        {
            if (entityState.DataBlobs.ContainsKey(typeof(EnergyGenAbilityDB)))
            {
                _entityState = entityState;
                _energyGenDB = (EnergyGenAbilityDB)entityState.DataBlobs[typeof(EnergyGenAbilityDB)];
                CanActive = true;
            }
            else
            {
                //CanActive = false;
                //_entityState = null;
            }
        }

        private void ManagerSubpulses_SystemDateChangedEvent(DateTime newdate)
        {
            //if we are looking at this, then we should process it even if nothing has changed.
            if (IsActive && CanActive)
            {
                if (_energyGenDB.dateTimeLastProcess != newdate)
                    EnergyGenProcessor.EnergyGen(_entityState.Entity, newdate);
            }
        }


        internal override void Display()
        {
            if(_entityState != _state.LastClickedEntity)
                SetEntity(_state.LastClickedEntity);
            if (IsActive && CanActive)
            {
                ImGui.Begin("Power Display " + _entityState.Name);
                ImGui.Text("Current Load: ");
                ImGui.SameLine();
                ImGui.Text(_energyGenDB.Load.ToString());
                
                ImGui.Text("Current Output: ");
                ImGui.SameLine();
                
                ImGui.Text(_energyGenDB.Output.ToString() + " / " + _energyGenDB.TotalOutputMax);
                
                ImGui.Text("Current Demand: ");
                ImGui.SameLine();
                ImGui.Text(_energyGenDB.Demand.ToString());
                
                ImGui.Text("Stored: ");
                ImGui.SameLine();
                string stor = _energyGenDB.EnergyStored[_energyGenDB.EnergyType.ID].ToString();
                string max = _energyGenDB.EnergyStoreMax[_energyGenDB.EnergyType.ID].ToString();
                ImGui.Text(stor + " / " + max);

                //

                //ImGui.PlotLines()
                var colour1 = ImGui.GetColorU32(ImGuiCol.Text);
                var colour2 = ImGui.GetColorU32(ImGuiCol.PlotLines);
                var colour3 = ImGui.GetColorU32(ImGuiCol.Button);
                ImDrawListPtr draw_list = ImGui.GetWindowDrawList();

                var plotPos = ImGui.GetCursorScreenPos();
                ImGui.InvisibleButton("PowerPlot", _plotSize);


                var hg = _energyGenDB.Histogram;
                
                int hgFirstIdx = _energyGenDB.HistogramIndex;
                int hgLastIdx;
                if (hgFirstIdx == 0)
                    hgLastIdx = hg.Count - 1;
                else
                    hgLastIdx = hgFirstIdx - 1;
            
                var hgFirstObj = hg[hgFirstIdx];
                var hgLastObj = hg[hgLastIdx];
                
                
                float xstep = _plotSize.X / hgLastObj.seconds ;
                float ystep = (float)(_plotSize.Y / _energyGenDB.EnergyStoreMax[_energyGenDB.EnergyType.ID]);
                float posX = 0;
                float posYBase = plotPos.Y + _plotSize.Y;
                int index = _energyGenDB.HistogramIndex;
                var thisData = _energyGenDB.Histogram[index];
                float posYO = ystep * (float)thisData.outputval;
                float posYD = ystep * (float)thisData.demandval;
                float posYS = ystep * (float)thisData.storval;
                //float ypos = plotPos.Y + _plotSize.Y;
                
                for (int i = 0; i < _energyGenDB.HistogramSize; i++)
                {
                    
                    int idx = index + i;
                    if (idx >= _energyGenDB.HistogramSize)
                        idx -= _energyGenDB.HistogramSize;
                    thisData = _energyGenDB.Histogram[idx];
                    
                    float nextX = xstep * thisData.seconds;
                    float nextYO = ystep * (float)thisData.outputval;
                    float nextYD = ystep * (float)thisData.demandval;
                    float nextYS = ystep * (float)thisData.storval;
                    draw_list.AddLine(new Vector2(plotPos.X + posX, posYBase - posYO), new Vector2(plotPos.X + nextX, posYBase - nextYO), colour1);
                    draw_list.AddLine(new Vector2(plotPos.X + posX, posYBase - posYD), new Vector2(plotPos.X + nextX, posYBase - nextYD), colour2);
                    draw_list.AddLine(new Vector2(plotPos.X + posX, posYBase - posYS), new Vector2(plotPos.X + nextX, posYBase - nextYS), colour3);
                    posX = nextX;
                    posYO = nextYO;
                    posYD = nextYD;
                    posYS = nextYS;
                }
                ImGui.End();
                
            }

        }
    }
}