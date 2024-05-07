private void ReturnTotalLeadTimeBlock(double[] PpValue, bool[] HasPlan, double QtyKeepCell, int LeadtimeBlock, int[] cellQty, ref double[] leadTime, ref double[] qtyKeepCell, ref double[] totalLeadtime)
{
    var arrActive = HasPlan.Select((value, index) => new { value, index })
              .Where(item => item.value == true)
              .Select(item => item.index)
              .ToArray();
    for (int i = 0; i < arrActive.Length; i++)
    {
        qtyKeepCell[arrActive[i]] = QtyKeepCell * cellQty[arrActive[i]];
        double sum = 0;
        for (int j = i; j < i + LeadtimeBlock - 1; j++)
        {
            try
            {
                sum += PpValue[arrActive[j]];
            }
            catch { };
        }
        leadTime[arrActive[i]] = sum;
        totalLeadtime[arrActive[i]] += sum + QtyKeepCell * cellQty[arrActive[i]];
    }
}
