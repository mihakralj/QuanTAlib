namespace QuanTAlib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;


public enum OType
{
    NIL = 0, // No position
    BTO = 1, // Buy to Open
    STC = 2, // Sell to Close
    STO = 3, // Sell to Open
    BTC = 4, // Buy to Close
    END = 5, // Exit the trade
}


public class TOrders : List<(DateTime t, OType o)>
{

    public void Add((DateTime t, OType o) TOrder, bool update = false)
    {
        if (update) { this[^1] = TOrder; }
        else { base.Add(TOrder); }
        OnEvent(update);
    }


    protected virtual void OnEvent(bool update = false)
    {
        Pub?.Invoke(this, new TSeriesEventArgs { update = update });
    }
    public delegate void NewDataEventHandler(object source, TSeriesEventArgs args);
    public event NewDataEventHandler Pub;

}