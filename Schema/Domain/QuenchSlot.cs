﻿namespace Schema.Domain;

public enum QuenchSlot : ushort
{
    Before,
    Objects, // Uses a loop before and after table quenches to resolve dependencies without requiring specific order and runs the scripts "ALWAYS"
    TableData, // Uses a loop before and after table quenches to resolve dependencies without requiring specific order and runs the scripts "ALWAYS"
    After
}