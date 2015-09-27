﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld;



namespace ToolsForHaul{
public static class Toils_Collect
{
    //Toil Collect

    public static Toil CollectInInventory(TargetIndex HaulableInd)
	{

		Toil toil = new Toil();
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            Thing haulThing = curJob.GetTarget(HaulableInd).Thing;

            //Check haulThing is human_corpse. If other race has apparel, It need to change
            if ((haulThing.ThingID.IndexOf("Human_Corpse") <= -1)? false : true)
            {
                Corpse corpse = (Corpse)haulThing;
                var wornApparel = corpse.innerPawn.apparel.WornApparel;

                //Drop wornApparel. wornApparel cannot Add to container directly because it will be duplicated.
                corpse.innerPawn.apparel.DropAll(corpse.innerPawn.Position, false);

                //Transfer in container
                foreach (Thing apparel in wornApparel)
                    actor.inventory.container.TryAdd(apparel);
            }
            //Collecting TargetIndex ind
            actor.inventory.container.TryAdd(haulThing);

        };
        toil.FailOn(() =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            Thing haulThing = curJob.GetTarget(HaulableInd).Thing;

            if (!actor.inventory.container.CanAcceptAnyOf(haulThing))
                return true;

            return false;
        });
		return toil;
	}

    public static Toil CollectInCarrier(TargetIndex CarrierInd, TargetIndex HaulableInd)
    {
        Toil toil = new Toil();
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            Thing haulThing = curJob.GetTarget(HaulableInd).Thing;
            Vehicle_Cart carrier = curJob.GetTarget(CarrierInd).Thing as Vehicle_Cart;
            //Check haulThing is human_corpse. If other race has apparel, It need to change

            Find.DesignationManager.RemoveAllDesignationsOn(haulThing);
            if ((haulThing.ThingID.IndexOf("Human_Corpse") <= -1) ? false : true)
            {
                Corpse corpse = (Corpse)haulThing;
                var wornApparel = corpse.innerPawn.apparel.WornApparel;

                //Drop wornApparel. wornApparel cannot Add to container directly because it will be duplicated.
                corpse.innerPawn.apparel.DropAll(corpse.innerPawn.Position, false);

                //Transfer in container
                foreach (Thing apparel in wornApparel)
                    carrier.storage.TryAdd(apparel);
            }
            //Collecting TargetIndex ind
            carrier.storage.TryAdd(haulThing);

            List<TargetInfo> thingList = curJob.GetTargetQueue(HaulableInd);
            for (int i = 0; i < thingList.Count; i++)
                if (actor.Position.AdjacentTo8Way(thingList[i].Thing.Position))
                {
                    Find.DesignationManager.RemoveAllDesignationsOn(thingList[i].Thing);
                    carrier.storage.TryAdd(thingList[i].Thing);
                    thingList.RemoveAt(i);
                    i--;
                }

        };
        toil.FailOn(() =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            Thing haulThing = curJob.GetTarget(HaulableInd).Thing;
            Vehicle_Cart carrier = curJob.GetTarget(CarrierInd).Thing as Vehicle_Cart;

            if (!carrier.storage.CanAcceptAnyOf(haulThing)
                && actor.Position.IsAdjacentTo8WayOrInside(haulThing.Position, haulThing.Rotation, haulThing.RotatedSize))
                return true;

            return false;
        });
        toil.FailOnDespawned(CarrierInd);
        return toil;
    }

    //Toil Drop

    public static Toil DropTheCarriedInCell(TargetIndex StoreCellInd, ThingPlaceMode placeMode)
    {
        Toil toil = new Toil();
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            if (actor.inventory.container.Count <= 0)
                return;
            Thing dropThing = actor.inventory.container.First();
            IntVec3 destLoc = actor.jobs.curJob.GetTarget(StoreCellInd).Cell;
            Thing dummy;

            Find.DesignationManager.RemoveAllDesignationsOn(dropThing);
            actor.inventory.container.TryDrop(dropThing, destLoc, placeMode, out dummy);

            return;
        };
        return toil;
    }

    public static Toil DropTheCarriedInCell(TargetIndex StoreCellInd, ThingPlaceMode placeMode, Thing lastItem)
    {
        Toil toil = new Toil();
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            if (actor.inventory.container.Count <= 0)
                return;

            //Thing dropThing = actor.inventory.container.First();
            Thing dropThing = null;
            if (lastItem != null)
            {
                for (int i = 0; i + 1 < actor.inventory.container.Count; i++)
                    if (actor.inventory.container[i] == lastItem)
                        dropThing = actor.inventory.container[i + 1];
            }
            else if (lastItem == null && actor.inventory.container.Count >= 1)
                dropThing = actor.inventory.container.First();

            if (dropThing == null)
            {
                //Log.Error(toil.actor + "try drop null thing in " + actor.jobs.curJob.GetTarget(StoreCellInd).Cell);
                return;
            }
            IntVec3 destLoc = actor.jobs.curJob.GetTarget(StoreCellInd).Cell;
            Thing dummy;

            Find.DesignationManager.RemoveAllDesignationsOn(dropThing);
            actor.inventory.container.TryDrop(dropThing, destLoc, placeMode, out dummy);

            return;
        };
        return toil;
    }

    public static Toil DropTheCarriedInCell(TargetIndex StoreCellInd, ThingPlaceMode placeMode, TargetIndex CarrierInd)
    {
        Toil toil = new Toil();
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            Vehicle_Cart carrier = actor.jobs.curJob.GetTarget(CarrierInd).Thing as Vehicle_Cart;
            if (carrier.storage.Count <= 0)
                return;
            Thing dropThing = carrier.storage.First(); 
            IntVec3 destLoc = actor.jobs.curJob.GetTarget(StoreCellInd).Cell;
            Thing dummy;

            Find.DesignationManager.RemoveAllDesignationsOn(dropThing);
            carrier.storage.TryDrop(dropThing, destLoc, placeMode, out dummy);

            //List<Thing> dropThings = carrier.storage.ToList();
            List<TargetInfo> cellList = curJob.GetTargetQueue(StoreCellInd);
            /*while (0 < cellList.Count && 0 < carrier.storage.Count)
                if (destLoc.AdjacentTo8Way(cellList.First().Cell))
                {
                    Find.DesignationManager.RemoveAllDesignationsOn(actor.inventory.container.First());
                    actor.inventory.container.TryDrop(actor.inventory.container.First(), cellList.First().Cell, ThingPlaceMode.Direct, out dummy);
                    cellList.RemoveAt(0);
                }
            */
            for (int i = 0; i < cellList.Count && 0 < cellList.Count && i < carrier.storage.Count && 0 < carrier.storage.Count; i++)
                if (destLoc.AdjacentTo8Way(cellList[i].Cell))
                {
                    Find.DesignationManager.RemoveAllDesignationsOn(carrier.storage[i]);
                    carrier.storage.TryDrop(carrier.storage[i], cellList[i].Cell, ThingPlaceMode.Direct, out dummy);
                    cellList.RemoveAt(i);
                    //dropThings.RemoveAt(dropThings.IndexOf(dropThings[i]));
                    i--;
                }

            return;
        };
        toil.FailOnDespawned(CarrierInd);
        return toil;
    }

    public static Toil DropAllInCell(TargetIndex StoreCellInd, ThingPlaceMode placeMode)
    {
        Toil toil = new Toil();
        toil.initAction = () =>
        {
            Pawn actor = toil.actor;
            Job curJob = actor.jobs.curJob;
            IntVec3 destLoc = actor.jobs.curJob.GetTarget(StoreCellInd).Cell;

            actor.inventory.container.TryDropAll(destLoc, placeMode);
            return;
        };
        return toil;
    }
                
}}

