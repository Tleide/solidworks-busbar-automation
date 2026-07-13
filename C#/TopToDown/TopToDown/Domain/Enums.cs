using System;

namespace SwFeatureDebug
{
    internal enum CabinetTopologyKind
    {
        TypicalDesign,
        SouthernGrid
    }

    internal enum ContactFace
    {
        Front,
        Back,
        Upper,
        Lower,
        Left,
        Right
    }

    internal enum BusbarWidthMode
    {
        MidPlane
    }

    internal enum ThicknessTransitionPolicy
    {
        PreferStartPort,
        PreferEndPort,
        Auto
    }

    internal enum ContactTopologyKind
    {
        SameSide,
        DifferentSide
    }

    internal enum PortKind
    {
        FuseOut,
        LoubaoIn,
        CollectorTap
    }

    internal enum RouteAxisOrder
    {
        YThenZ,
        ZThenY
    }

    internal enum BranchRouteMode
    {
        Standard,
        DoubleClampOuterAvoidance
    }

    internal enum BusbarKind
    {
        MainFeed,
        Collector,
        Branch
    }

    internal enum BranchArrangement
    {
        Single,
        DoubleClamp
    }

    internal enum BranchLegRole
    {
        Single,
        Lower,
        Upper
    }

    internal enum SheetMetalWidthSide
    {
        Center,
        Positive,
        Negative
    }

    internal enum AxisDirection
    {
        X,
        Y,
        Z
    }
}
