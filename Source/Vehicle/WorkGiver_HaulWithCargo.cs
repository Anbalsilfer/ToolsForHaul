﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using Verse.AI;


namespace ToolsForHaul
{
    public class WorkGiver_HaulWithCargo : WorkGiver
    {
        private static List<Thing> availableVehicle;
        private static IntVec3 invalidCell = new IntVec3(0, 0, 0);


        public WorkGiver_HaulWithCargo() : base() {}
        /*
        public virtual PathEndMode PathEndMode { get; }
        public virtual ThingRequest PotentialWorkThingRequest { get; }

        public virtual bool HasJobOnCell(Pawn pawn, IntVec3 c);
        public virtual bool HasJobOnThing(Pawn pawn, Thing t);
        public virtual Job JobOnCell(Pawn pawn, IntVec3 cell);
        public virtual Job JobOnThing(Pawn pawn, Thing t);
        public PawnActivityDef MissingRequiredActivity(Pawn pawn);
        public virtual IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn);
        public virtual IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn Pawn);
        public virtual bool ShouldSkip(Pawn pawn);
         */
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            availableVehicle = Find.ListerThings.AllThings.FindAll((Thing aV)
            => ((aV is Vehicle_Cargo) && !aV.IsForbidden(pawn.Faction)
            && ((!aV.TryGetComp<CompMountable>().IsMounted && pawn.CanReserve(aV))   //Unmounted
                || aV.TryGetComp<CompMountable>().Driver == pawn)                  //or Driver is pawnself
            ));

            #if DEBUG
            //Log.Message("Number of Reservation:" + Find.Reservations.AllReservedThings().Count().ToString());
            //Log.Message("availableVehicle Count: " + availableVehicle.Count);
            #endif
            return availableVehicle as IEnumerable<Thing>;
        }

        public override bool ShouldSkip(Pawn pawn)
        {
            availableVehicle = Find.ListerThings.AllThings.FindAll((Thing aV)
            => ((aV is Vehicle_Cargo) && !aV.IsForbidden(pawn.Faction)
            && ((!aV.TryGetComp<CompMountable>().IsMounted && pawn.CanReserve(aV))   //Unmounted
                || aV.TryGetComp<CompMountable>().Driver == pawn)                  //or Driver is pawnself
            ));

            return (availableVehicle.Find(aV => ((Vehicle_Cargo)aV).storage.TotalStackCount > 0) == null
                    && ListerHaulables.ThingsPotentiallyNeedingHauling().Count == 0);        //No Haulable
        }

        public override Job JobOnThing(Pawn pawn, Thing t)
        {
            #if DEBUG
            Log.Message("In " + System.Reflection.MethodBase.GetCurrentMethod() + " Memory usage: " + GC.GetTotalMemory(false));
            #endif

            Vehicle_Cargo carrier = t as Vehicle_Cargo;
            if (carrier == null)
                return null;

            IEnumerable<Thing> remainingItems = carrier.storage.Contents;
            //Note: For avoiding error check in PositionHeld(), it should be fullcopy.
            List<Thing> haulables = ListerHaulables.ThingsPotentiallyNeedingHauling().FindAll(thing => carrier.allowances.Allows(thing.def)).ListFullCopyOrNull();
            int reservedMaxItem = carrier.storage.Contents.Count();
            Job jobCollect = new Job(DefDatabase<JobDef>.GetNamed("Collect"));
            //jobCollect.maxNumToCarry = 99999;
            //jobCollect.haulMode = HaulMode.ToCellStorage;
            jobCollect.targetQueueA = new List<TargetInfo>();
            jobCollect.targetQueueB = new List<TargetInfo>();

            //Set carrier
            jobCollect.targetC = carrier;
            ReservationUtility.Reserve(pawn, carrier);

            //Drop remaining item
            foreach (var remainingItem in remainingItems)
            {
                IntVec3 storageCell = FindStorageCell(pawn, remainingItem, jobCollect.targetQueueB);
                if (!storageCell.IsValid) break;
                
                ReservationUtility.Reserve(pawn, storageCell);
                jobCollect.targetQueueB.Add(storageCell);
            }
            if (!jobCollect.targetQueueB.NullOrEmpty())
            {
                #if DEBUG
                Log.Message("In End of" + System.Reflection.MethodBase.GetCurrentMethod() + " Memory usage: " + GC.GetTotalMemory(false));
                #endif
                return jobCollect;
            }
            //collectThing Predicate
            Predicate<Thing> predicate = item
                => pawn.CanReserve(item) && !item.IsInValidBestStorage();

            //Collect and drop item
            while (reservedMaxItem < carrier.maxItem)
            {
                IntVec3 storageCell = IntVec3.Invalid;
                Thing closestHaulable = null;

                IntVec3 searchPos;
                searchPos = (!jobCollect.targetQueueA.NullOrEmpty() && jobCollect.targetQueueA.First() != IntVec3.Invalid) ?
                    jobCollect.targetQueueA.First().Thing.Position : searchPos = carrier.Position;

                closestHaulable = GenClosest.ClosestThing_Global_Reachable(searchPos,
                                                                            haulables,
                                                                            PathEndMode.Touch,
                                                                            TraverseParms.For(pawn),
                                                                            9999,
                                                                            predicate);
                if (closestHaulable == null) break;

                storageCell = FindStorageCell(pawn, closestHaulable, jobCollect.targetQueueB);
                if (storageCell == IntVec3.Invalid) break;
                jobCollect.targetQueueA.Add(closestHaulable);
                jobCollect.targetQueueB.Add(storageCell);
                ReservationUtility.Reserve(pawn, closestHaulable);
                ReservationUtility.Reserve(pawn, storageCell);
                haulables.Remove(closestHaulable);
                reservedMaxItem++;
            }

            //Has job
            if (!jobCollect.targetQueueA.NullOrEmpty() && !jobCollect.targetQueueB.NullOrEmpty())
            {
                #if DEBUG
                Log.Message("In End of" + System.Reflection.MethodBase.GetCurrentMethod() + " Memory usage: " + GC.GetTotalMemory(false));
                #endif
                return jobCollect;
            }

            //No haulables or zone. Release everything
            Find.Reservations.ReleaseAllClaimedBy(pawn);

            #if DEBUG
            Log.Message("In End of" + System.Reflection.MethodBase.GetCurrentMethod() + " Memory usage: " + GC.GetTotalMemory(false));
            #endif
            return null;
        }


        private IntVec3 FindStorageCell(Pawn pawn, Thing closestHaulable, List<TargetInfo> targetQueue)
        {
            if (!targetQueue.NullOrEmpty())
                foreach (TargetInfo target in targetQueue)
                    foreach (var adjCell in GenAdjFast.AdjacentCells8Way(target))
                        if (!targetQueue.Contains(adjCell) && adjCell.IsValidStorageFor(closestHaulable) && pawn.CanReserve(adjCell))
                            return adjCell;

            foreach (var slotGroup in Find.SlotGroupManager.AllGroupsListInPriorityOrder)
            {
                foreach (var cell in slotGroup.CellsList.Where(cell =>
                            !targetQueue.Contains(cell) && StoreUtility.IsValidStorageFor(cell, closestHaulable) && pawn.CanReserve(cell)))
                    if (cell != invalidCell && cell != IntVec3.Invalid)
                        return cell;
            }

            return IntVec3.Invalid;
        }

    }

}