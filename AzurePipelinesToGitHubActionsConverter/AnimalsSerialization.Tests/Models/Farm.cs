﻿namespace AnimalSerialization.Tests.Models
{
    public class Farm<TFarmItem1, TFarmItem2>
    {
        public TFarmItem1 FarmItem1 { get; set; }
        public TFarmItem2 FarmItem2 { get; set; }
    }
}
