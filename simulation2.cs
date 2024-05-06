public async Task PushLeadtime()
{
    //var lstResult = new List<SimualationPartmasterByCellView>();
    var md1 = await _ctx.VSimulationLeadtimeLbpS1Findmers/*.Take(20)*/.ToListAsync();
    var lstDetail = await _ctx.VSimulationPartDemandByCellLbps.ToListAsync();
    var lstCell = await _ctx.VSimulationCountCellByShiftLbps.AsNoTracking().ToListAsync();
    var lstScanDate = new List<SimualationPartmasterTimeView>();
    foreach (var s in lstBlockTimeAll)
    {
        lstScanDate.Add(new SimualationPartmasterTimeView() { TimeF = s });
    }
    
    var lstLeadTime = await _ctx.VSimulationLeadtimeTableLbps.ToListAsync();
    ConcurrentDictionary<(string, string), (double,int)> dictLeadtime = new ConcurrentDictionary<(string, string), (double, int)>();
    foreach (var dtail in lstLeadTime)
    {
        dictLeadtime.AddOrUpdate((dtail.PartNo ?? "", dtail.Model ?? ""), (dtail.QtyKeepCell ?? 0, dtail.LeadtimeBlock.ObjToIntAble()), (key, oldValue) => (dtail.QtyKeepCell ?? 0, dtail.LeadtimeBlock.ObjToIntAble()));
    }
    ConcurrentDictionary<(string, string,string), int> dictCountCell = new ConcurrentDictionary<(string, string, string), int>();
    foreach (var c in lstCell)
    {
        dictCountCell.AddOrUpdate((c.ProductCode ?? "", c.PalletDate ?? "", c.Shift ?? ""), c.LineNo.ObjToIntAble(), (key, oldValue) => c.LineNo.ObjToIntAble());
    }
    ConcurrentDictionary<string, int> dicPositionMerchandise = new ConcurrentDictionary<string, int>();
    var header = await _ctx.TodStructureOutputHeaders.Where(x => x.Active == true && x.Product == "LBP")
        .Select(x => new { Model = x.Model, Merchandise = x.Merchandise, MerCode = x.MerCode })
        .AsNoTracking()
        .FirstAsync();
    foreach (var h in header.MerCode)
    {
        var arh = h.Split(':');
        dicPositionMerchandise.AddOrUpdate(arh[1], arh[0].ObjToIntAble(), (key, val) => arh[0].ObjToIntAble());
    }
    //Console.WriteLine(DateTime.Now.ToLongTimeString());
    var dtString = DateTime.Today.ToString("dd-MM-yyyy");
    var aiTime = await _ctx.VHksAiRestTimeLbps/*.Where(x => x.PalletDate == dtString)*/.AsNoTracking().ToListAsync();
    var lstDemandByCell = new List<SimualationPartmasterByCellView>();
    foreach (var p in md1)
    {
        var part = p.PartNo;
        var mer = p.ArrMerchandise;
        var lstDetail1 = lstDetail.Where(x => mer.Contains(x.Merchandise)).ToList();//*usage*ratio/100
        if(lstDetail1 == null || lstDetail1.Count < 1)
        {
            continue;
        }
        ConcurrentDictionary<(string, string, string, string), double> dictQty = new ConcurrentDictionary<(string, string, string, string), double>();
        ConcurrentDictionary<string, double> dictUsage = new ConcurrentDictionary<string, double>();
        foreach (var m in p.ArrMerchandise.Distinct())
        {
            if(dicPositionMerchandise.TryGetValue(m, out int usagePos))
            {
                double valUsage = p.Usage[usagePos].ObjToDoubleAble() * p.Ratio.ObjToDoubleAble() / 100;
                dictUsage.TryAdd(m, valUsage);
            }
        }
        
        foreach (var dtail in lstDetail1)
        {
            dictQty.AddOrUpdate((dtail.Model ?? "", dtail.PalletDate ?? "", dtail.LineNo ?? "", dtail.BTime ?? ""), (dtail.Vqty ?? 0) * dictUsage[dtail.Merchandise], (key, oldValue) => oldValue + (dtail.Vqty ?? 0) * dictUsage[dtail.Merchandise]);
        }
        var lstLine = lstDetail1.Select(x => new { x.Model, x.LineNo }).Distinct().ToList();
        var lstLineShift = lstDetail1.Select(x => new { x.Model, x.LineNo, x.Shift }).Distinct().ToList();
        foreach (var l in lstLine)
        {
            var aiItem = aiTime.Where(a => a.LineNo.StartsWith(l.LineNo)).ToList();
            if(aiItem == null || aiTime.Count < 1)
            {
                continue;
            }
            var lstBlockRangeRestTime = aiItem.Where(a => a.RestTime != null && a.RestTime.Length > 0).SelectMany(a => a.RestTime).ToArray();
            if(lstBlockRangeRestTime == null || lstBlockRangeRestTime.Length < 1)
            {
                continue;
            }
            var lstBreakEachBlockTime = lstBlockRangeRestTime.ReturnRestBlockTime();
            double[] demand = new double[lstScanDate.Count];
            int[] cellQty = new int[lstScanDate.Count];
            bool[] hasPlan = new bool[lstScanDate.Count];
            double[] leadTime = new double[lstScanDate.Count];
            double[] qtyKeepCell = new double[lstScanDate.Count];
            double[] totalLeadtime = new double[lstScanDate.Count];
            var lstShift = lstLineShift.Where(b => b.Model == l.Model && b.LineNo == l.LineNo).Select(b => b.Shift).ToList();
            dictLeadtime.TryGetValue((part, l.Model), out var b);
            for (int i = 0; i < lstScanDate.Count; i++)
            {
                var d = lstScanDate[i];
                var stBlock = d.BTime.Substring(11, 5);
                if(lstBreakEachBlockTime.Contains(stBlock))
                {
                    hasPlan[i] = false;
                    continue;
                }
                else
                {
                    dictQty.TryGetValue((l.Model, d.PalletDate, l.LineNo, d.BTime), out demand[i]);
                    hasPlan[i] = lstShift.Contains(d.Shift);
                    dictCountCell.TryGetValue((l.Model, d.PalletDate, d.Shift), out cellQty[i]);
                }
            }
            ReturnTotalLeadTimeBlock(demand, hasPlan, b.Item1, b.Item2, cellQty, ref leadTime, ref qtyKeepCell, ref totalLeadtime);
            lstDemandByCell.Add(new SimualationPartmasterByCellView()
            {
                Cell = l.LineNo,
                PartNo = part,
                DemandArr = demand,
                Model = l.Model,
                HasPlanArr = hasPlan,
                QtyKeepCell = b.Item1,
                LeadtimeBlock = b.Item2,
                LeadtimeArr = leadTime,
                QtyKeepCellArr = qtyKeepCell,
                TotalLeadTimeArr = totalLeadtime,
                Vendor = p.Vendor,
                RatioChangeRatio = p.RatioChange,
                RatioChangeEffectiveDate = p.EffectivedateChange,
                Ratio = p.Ratio,
                PicPo = p.PoPic,
                PicDo = p.DoPic,
                Bc = p.Bc,
                Dim = p.Dim,
                PartName = p.PartName
            });
            Console.WriteLine($"Da tinh xong line {l.LineNo}: {DateTime.Now.ToString("HH:mm:ss.fff")}");
        }
        //Console.WriteLine($"Da tinh xong part {part}: {DateTime.Now.ToString("HH:mm:ss.fff")}");
    }
    Console.WriteLine($"Da tinh xong all part: {DateTime.Now.ToString("HH:mm:ss.fff")}");
    if(lstDemandByCell != null && lstDemandByCell.Count > 0)
    {
        var lstResult = new List<PcSimulationLeadtimeLbp>();
        foreach(var itcd in lstDemandByCell)
        {
            lstResult.Add(new PcSimulationLeadtimeLbp()
            {
                PartNo = itcd.PartNo,
                Model = itcd.Model,
                Leadtime = itcd.LeadtimeBlock,
                TotalLt = itcd.TotalLeadTimeArr,
                QtyKeepCell = itcd.QtyKeepCell,
                QtyKeepArr = itcd.QtyKeepCellArr,
                LtValue = itcd.LeadtimeArr,
                DateVal = lstBlockTimeAll,
                DayQty = 12,
                HasPlan = itcd.HasPlanArr,
                Bc = itcd.Bc,
                Destination = "",
                Cell = itcd.Cell,
                Dim = itcd.Dim,
                PartName = itcd.PartName,
                PicDo = itcd.PicDo,
                PicPo = itcd.PicPo,
                Ratio = itcd.Ratio,
                RatioChangeEffectiveDate = itcd.RatioChangeEffectiveDate,
                RatioChangeRatio = itcd.RatioChangeRatio,
                Vendor = itcd.Vendor,
                DemandArr = itcd.DemandArr
            });
        }
        await _ctx.PcSimulationLeadtimeLbps.AddRangeAsync(lstResult);
        await _ctx.SaveChangesAsync();
    }
    
}

private void ReturnTotalLeadTimeBlock(double[] PpValue, bool[] HasPlan, double QtyKeepCell, int LeadtimeBlock, int[] cellQty, ref double[] leadTime, ref double[] qtyKeepCell, ref double[] totalLeadtime)
{
    var arrActive = HasPlan.Select((value, index) => new { value, index })
              .Where(item => item.value == true)
              .Select(item => item.index)
              .ToArray();
    for (int i = 0; i < arrActive.Length; i++)
    {
        qtyKeepCell[arrActive[i]] = QtyKeepCell * cellQty[i];
        double sum = 0;
        for (int j = i; j < i + LeadtimeBlock; j++)
        {
            try
            {
                sum += PpValue[arrActive[j]];
            }
            catch { };
        }
        leadTime[arrActive[i]] = sum;
        totalLeadtime[arrActive[i]] += sum + QtyKeepCell * cellQty[i];
    }
}
