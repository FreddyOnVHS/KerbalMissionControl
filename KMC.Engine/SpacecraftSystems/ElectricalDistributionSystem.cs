using System;
using System.Diagnostics;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Build 14.11.3 synthetic DC distribution with explicit switching truth.
    /// Generator is primary, battery is standby/reserve, and automatic source
    /// transfer selects the battery only when the primary generator cannot feed
    /// the bus. KSP ElectricCharge remains separate observed physical truth.
    /// </summary>
    public sealed class SyntheticElectricalDistributionSystem
    {
        private const string DistributionTemplateId = "KMC-14.11.3-28V-DC-SWITCHED";
        private const double NominalVoltage = 28.0;
        private const double HighLoadThreshold = 0.80;
        private const double UndervoltageThreshold = 24.0;

        public SyntheticElectricalDistributionModel BuildAndApply(
            SpacecraftSystemsModel systems,
            DateTime generatedUtc,
            ElectricalControlSnapshot controls,
            FailureSimulationSnapshot failures)
        {
            SyntheticElectricalDistributionModel distribution = BuildNominalDistribution(generatedUtc);
            ApplyCrewControls(distribution, systems, controls);
            SyntheticFailureEngine.ApplyElectricalSourceFailures(distribution, failures);
            ResolveSwitching(distribution);
            Recalculate(distribution);
            ApplyBusStatesToSystems(systems, distribution);
            return distribution;
        }

        internal static void Recalculate(SyntheticElectricalDistributionModel distribution)
        {
            if (distribution == null) return;

            for (int i=0;i<distribution.Buses.Count;i++)
            {
                SyntheticElectricalBus bus=distribution.Buses[i];
                if (bus==null) continue;
                bus.DemandAmps=SumDemand(distribution,bus.Id);
                bus.AvailableCurrentAmps=0.0;
                bus.ActiveSourceCount=0;
                bus.Voltage=0.0;
                bus.State=SyntheticElectricalBusState.Unpowered;
            }

            int maximumPasses=Math.Max(1,distribution.Buses.Count+1);
            for (int pass=0;pass<maximumPasses;pass++)
            {
                bool changed=false;
                ResolveSwitching(distribution);

                for (int i=0;i<distribution.Buses.Count;i++)
                {
                    SyntheticElectricalBus bus=distribution.Buses[i];
                    if (bus==null) continue;
                    double available=0.0;
                    double sourceVoltage=0.0;
                    int sourceCount=0;
                    string activeSourceId=string.Empty;

                    for (int s=0;s<distribution.Sources.Count;s++)
                    {
                        SyntheticElectricalSource source=distribution.Sources[s];
                        if (source==null || !string.Equals(source.BusId,bus.Id,StringComparison.Ordinal) || !IsSourceUsable(distribution,source)) continue;
                        double current=source.AvailableCurrentAmps;
                        if (current<=0.000001) continue;
                        available += current;
                        sourceVoltage=Math.Max(sourceVoltage,source.NominalVoltage);
                        sourceCount++;
                        if (string.IsNullOrWhiteSpace(activeSourceId)) activeSourceId=source.Id;
                    }

                    SyntheticElectricalBusState nextState;
                    double nextVoltage;
                    CalculateBusState(bus.NominalVoltage,bus.DemandAmps,available,sourceVoltage,out nextState,out nextVoltage);
                    if (Math.Abs(bus.AvailableCurrentAmps-available)>0.000001 || Math.Abs(bus.Voltage-nextVoltage)>0.000001 || bus.ActiveSourceCount!=sourceCount || bus.State!=nextState || !string.Equals(bus.ActiveSourceId,activeSourceId,StringComparison.Ordinal))
                    {
                        bus.AvailableCurrentAmps=available;
                        bus.ActiveSourceCount=sourceCount;
                        bus.Voltage=nextVoltage;
                        bus.State=nextState;
                        bus.ActiveSourceId=activeSourceId;
                        changed=true;
                    }
                }
                if (!changed) break;
            }
        }

        private static void ResolveSwitching(SyntheticElectricalDistributionModel d)
        {
            if (d==null) return;
            for (int i=0;i<d.Sources.Count;i++)
            {
                SyntheticElectricalSource s=d.Sources[i];
                if (s!=null) { s.SelectedForBus=false; s.Conducting=false; }
            }
            for (int i=0;i<d.Switches.Count;i++)
            {
                SyntheticElectricalSwitch sw=d.Switches[i];
                if (sw!=null) { sw.ActualClosed=sw.CommandedClosed; sw.IndicatedClosed=sw.ActualClosed; sw.Conducting=false; }
            }

            ResolveMainSourceTransfer(d,"BUS_MAIN_A","SRC_GEN_A","SRC_BAT_A","XFER_MAIN_A");
            ResolveMainSourceTransfer(d,"BUS_MAIN_B","SRC_GEN_B","SRC_BAT_B","XFER_MAIN_B");

            ResolveFeed(d,"FEED_ESS_A");
            ResolveFeed(d,"FEED_ESS_B");

            for (int i=0;i<d.Loads.Count;i++)
            {
                SyntheticElectricalLoad load=d.Loads[i];
                if (load==null) continue;
                SyntheticElectricalSwitch brk=d.FindSwitch(load.BreakerId);
                if (brk!=null)
                {
                    brk.CommandedClosed=load.CommandedOn;
                    brk.ActualClosed=brk.CommandedClosed;
                    brk.IndicatedClosed=brk.ActualClosed;
                    brk.Conducting=brk.ActualClosed;
                }
            }
        }

        private static void ResolveMainSourceTransfer(SyntheticElectricalDistributionModel d,string busId,string genId,string batId,string xferId)
        {
            SyntheticElectricalSource gen=d.FindSource(genId);
            SyntheticElectricalSource bat=d.FindSource(batId);
            SyntheticElectricalSwitch genCont=gen!=null?d.FindSwitch(gen.ContactorId):null;
            SyntheticElectricalSwitch batCont=bat!=null?d.FindSwitch(bat.ContactorId):null;
            SyntheticElectricalSwitch xfer=d.FindSwitch(xferId);

            bool genReady=SourceHardwareReady(gen,genCont);
            bool batReady=SourceHardwareReady(bat,batCont);
            SyntheticElectricalSource selected=genReady?gen:(batReady?bat:null);

            if (xfer!=null)
            {
                xfer.CommandedClosed=selected!=null;
                xfer.ActualClosed=xfer.CommandedClosed;
                xfer.IndicatedClosed=xfer.ActualClosed;
                xfer.Conducting=xfer.ActualClosed && selected!=null;
                xfer.UpstreamId=selected!=null?selected.Id:string.Empty;
                xfer.DownstreamId=busId;
            }

            if (selected!=null)
            {
                selected.SelectedForBus=true;
                selected.Conducting=xfer==null || xfer.Conducting;
                SyntheticElectricalSwitch cont=d.FindSwitch(selected.ContactorId);
                if (cont!=null) cont.Conducting=selected.Conducting;
            }
        }

        private static bool SourceHardwareReady(SyntheticElectricalSource s,SyntheticElectricalSwitch cont)
        {
            if (s==null || !s.CommandedAvailable || s.State==SyntheticElectricalSourceState.Offline || s.RatedAvailableCurrentAmps<=0.000001) return false;
            return cont==null || cont.ActualClosed;
        }

        private static void ResolveFeed(SyntheticElectricalDistributionModel d,string sourceId)
        {
            SyntheticElectricalSource feed=d.FindSource(sourceId);
            if (feed==null) return;
            SyntheticElectricalSwitch cont=d.FindSwitch(feed.ContactorId);
            bool closed=cont==null || cont.ActualClosed;
            SyntheticElectricalBus parent=d.FindBus(feed.ParentBusId);
            bool parentUsable=parent!=null && (parent.State==SyntheticElectricalBusState.Nominal || parent.State==SyntheticElectricalBusState.HighLoad);
            feed.SelectedForBus=closed && feed.CommandedAvailable && feed.State!=SyntheticElectricalSourceState.Offline && parentUsable;
            feed.Conducting=feed.SelectedForBus;
            if (cont!=null) cont.Conducting=feed.Conducting;
        }

        private static SyntheticElectricalDistributionModel BuildNominalDistribution(DateTime generatedUtc)
        {
            SyntheticElectricalDistributionModel d=new SyntheticElectricalDistributionModel { TemplateId=DistributionTemplateId, GeneratedUtc=generatedUtc };
            AddBus(d,"BUS_MAIN_A","MAIN BUS A","XFER_MAIN_A");
            AddBus(d,"BUS_MAIN_B","MAIN BUS B","XFER_MAIN_B");
            AddBus(d,"BUS_ESS","ESSENTIAL BUS",string.Empty);

            AddSource(d,"SRC_GEN_A","GENERATOR A","BUS_MAIN_A",string.Empty,SyntheticElectricalSourceKind.Generator,12.0,"CONT_GEN_A");
            AddSource(d,"SRC_BAT_A","BATTERY A","BUS_MAIN_A",string.Empty,SyntheticElectricalSourceKind.Battery,6.0,"CONT_BAT_A");
            AddSource(d,"SRC_GEN_B","GENERATOR B","BUS_MAIN_B",string.Empty,SyntheticElectricalSourceKind.Generator,12.0,"CONT_GEN_B");
            AddSource(d,"SRC_BAT_B","BATTERY B","BUS_MAIN_B",string.Empty,SyntheticElectricalSourceKind.Battery,6.0,"CONT_BAT_B");
            AddSource(d,"FEED_ESS_A","ESS FEED A","BUS_ESS","BUS_MAIN_A",SyntheticElectricalSourceKind.BusFeed,6.0,"CONT_ESS_A");
            AddSource(d,"FEED_ESS_B","ESS FEED B","BUS_ESS","BUS_MAIN_B",SyntheticElectricalSourceKind.BusFeed,6.0,"CONT_ESS_B");

            AddSwitch(d,"CONT_GEN_A","GEN A CONTACTOR","SRC_GEN_A","XFER_MAIN_A",SyntheticElectricalSwitchKind.SourceContactor,false);
            AddSwitch(d,"CONT_BAT_A","BAT A CONTACTOR","SRC_BAT_A","XFER_MAIN_A",SyntheticElectricalSwitchKind.SourceContactor,false);
            AddSwitch(d,"XFER_MAIN_A","MAIN A SOURCE TRANSFER","","BUS_MAIN_A",SyntheticElectricalSwitchKind.SourceTransfer,true);
            AddSwitch(d,"CONT_GEN_B","GEN B CONTACTOR","SRC_GEN_B","XFER_MAIN_B",SyntheticElectricalSwitchKind.SourceContactor,false);
            AddSwitch(d,"CONT_BAT_B","BAT B CONTACTOR","SRC_BAT_B","XFER_MAIN_B",SyntheticElectricalSwitchKind.SourceContactor,false);
            AddSwitch(d,"XFER_MAIN_B","MAIN B SOURCE TRANSFER","","BUS_MAIN_B",SyntheticElectricalSwitchKind.SourceTransfer,true);
            AddSwitch(d,"CONT_ESS_A","ESS FEED A CONTACTOR","BUS_MAIN_A","BUS_ESS",SyntheticElectricalSwitchKind.BusFeedContactor,false);
            AddSwitch(d,"CONT_ESS_B","ESS FEED B CONTACTOR","BUS_MAIN_B","BUS_ESS",SyntheticElectricalSwitchKind.BusFeedContactor,false);

            AddLoad(d,"GUID_A","GUID COMPUTER A","BUS_MAIN_A",2.0,1);
            AddLoad(d,"COMM_A","COMM TRANSCEIVER A","BUS_MAIN_A",1.5,2);
            AddLoad(d,"PUMP_A","PROP FEED PUMP A","BUS_MAIN_A",4.0,2);
            AddLoad(d,"FLIGHT_COMPUTER","PRIMARY FLIGHT COMPUTER","BUS_ESS",3.0,1);
            AddLoad(d,"GUID_B","GUID COMPUTER B","BUS_MAIN_B",2.0,1);
            AddLoad(d,"COMM_B","COMM TRANSCEIVER B","BUS_MAIN_B",1.5,2);
            AddLoad(d,"PUMP_B","PROP FEED PUMP B","BUS_MAIN_B",4.0,2);
            return d;
        }

        private static void ApplyCrewControls(SyntheticElectricalDistributionModel d,SpacecraftSystemsModel systems,ElectricalControlSnapshot controls)
        {
            if (d==null || controls==null) return;
            for (int i=0;i<d.Sources.Count;i++)
            {
                SyntheticElectricalSource s=d.Sources[i]; if (s==null) continue;
                bool commanded;
                if (controls.TryGet(s.Id,out commanded))
                {
                    s.CommandedAvailable=commanded;
                    SyntheticElectricalSwitch cont=d.FindSwitch(s.ContactorId);
                    if (cont!=null) cont.CommandedClosed=commanded;
                }
            }
            for (int i=0;i<d.Loads.Count;i++)
            {
                SyntheticElectricalLoad load=d.Loads[i]; if (load==null) continue;
                bool commanded;
                if (!controls.TryGet(load.EquipmentId,out commanded)) continue;
                load.CommandedOn=commanded;
                SyntheticElectricalSwitch brk=d.FindSwitch(load.BreakerId);
                if (brk!=null) brk.CommandedClosed=commanded;
                if (systems!=null)
                {
                    SpacecraftSystemComponent c=systems.FindComponent(load.EquipmentId);
                    if (c!=null) c.CommandedOn=commanded;
                }
            }
        }

        private static void ApplyBusStatesToSystems(SpacecraftSystemsModel systems,SyntheticElectricalDistributionModel d)
        {
            if (systems==null || d==null) return;
            for (int i=0;i<systems.Components.Count;i++) if (systems.Components[i]!=null) systems.Components[i].ProviderStateOverride=null;
            for (int i=0;i<d.Buses.Count;i++)
            {
                SyntheticElectricalBus bus=d.Buses[i]; if (bus==null) continue;
                SpacecraftSystemComponent c=systems.FindComponent(bus.Id); if (c==null) continue;
                c.ProviderStateOverride=ConvertBusState(bus.State);
            }
            systems.Recalculate();
        }

        private static SpacecraftSystemState ConvertBusState(SyntheticElectricalBusState state)
        {
            switch(state)
            {
                case SyntheticElectricalBusState.Unpowered: return SpacecraftSystemState.Unpowered;
                case SyntheticElectricalBusState.Overloaded:
                case SyntheticElectricalBusState.Undervoltage: return SpacecraftSystemState.Degraded;
                default: return SpacecraftSystemState.Online;
            }
        }

        private static bool IsSourceUsable(SyntheticElectricalDistributionModel d,SyntheticElectricalSource s)
        {
            if (s==null || !s.Conducting || s.State==SyntheticElectricalSourceState.Offline) return false;
            if (s.Kind!=SyntheticElectricalSourceKind.BusFeed) return true;
            SyntheticElectricalBus parent=d.FindBus(s.ParentBusId);
            return parent!=null && (parent.State==SyntheticElectricalBusState.Nominal || parent.State==SyntheticElectricalBusState.HighLoad);
        }

        private static double SumDemand(SyntheticElectricalDistributionModel d,string busId)
        {
            double demand=0.0;
            for (int i=0;i<d.Loads.Count;i++)
            {
                SyntheticElectricalLoad load=d.Loads[i];
                if (load!=null && load.CommandedOn && string.Equals(load.BusId,busId,StringComparison.Ordinal)) demand+=Math.Max(0.0,load.DemandAmps);
            }
            return demand;
        }

        private static void CalculateBusState(double nominal,double demand,double available,double sourceVoltage,out SyntheticElectricalBusState state,out double voltage)
        {
            if (available<=0.000001) { state=SyntheticElectricalBusState.Unpowered; voltage=0.0; return; }
            double source=sourceVoltage>0.000001?sourceVoltage:nominal;
            double fraction=demand/available;
            if (fraction>1.0)
            {
                voltage=source*Math.Max(0.70,available/Math.Max(demand,0.000001));
                state=voltage<UndervoltageThreshold?SyntheticElectricalBusState.Undervoltage:SyntheticElectricalBusState.Overloaded;
                return;
            }
            voltage=source;
            state=fraction>=HighLoadThreshold?SyntheticElectricalBusState.HighLoad:SyntheticElectricalBusState.Nominal;
        }

        private static void AddBus(SyntheticElectricalDistributionModel d,string id,string name,string transferId)
        {
            d.Buses.Add(new SyntheticElectricalBus { Id=id, DisplayName=name, TransferSwitchId=transferId, NominalVoltage=NominalVoltage });
        }
        private static void AddSource(SyntheticElectricalDistributionModel d,string id,string name,string bus,string parent,SyntheticElectricalSourceKind kind,double amps,string contactor)
        {
            d.Sources.Add(new SyntheticElectricalSource { Id=id, DisplayName=name, BusId=bus, ParentBusId=parent, ContactorId=contactor, Kind=kind, CommandedAvailable=true, State=SyntheticElectricalSourceState.Online, NominalVoltage=NominalVoltage, CapacityAmps=amps });
        }
        private static void AddSwitch(SyntheticElectricalDistributionModel d,string id,string name,string upstream,string downstream,SyntheticElectricalSwitchKind kind,bool automatic)
        {
            d.Switches.Add(new SyntheticElectricalSwitch { Id=id, DisplayName=name, UpstreamId=upstream, DownstreamId=downstream, Kind=kind, CommandedClosed=true, ActualClosed=true, IndicatedClosed=true, Automatic=automatic });
        }
        private static void AddLoad(SyntheticElectricalDistributionModel d,string id,string name,string bus,double amps,int priority)
        {
            string breaker="BRK_"+id;
            d.Loads.Add(new SyntheticElectricalLoad { EquipmentId=id, DisplayName=name, BusId=bus, BreakerId=breaker, DemandAmps=amps, Priority=priority, CommandedOn=true });
            AddSwitch(d,breaker,name+" BREAKER",bus,id,SyntheticElectricalSwitchKind.LoadBreaker,false);
        }
    }
}
