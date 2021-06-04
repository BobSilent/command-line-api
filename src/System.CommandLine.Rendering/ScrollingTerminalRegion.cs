// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Rendering
{
    internal class ScrollingTerminalRegion : Region
    {
        public ScrollingTerminalRegion() : base(left: Console.IsOutputRedirected
                                                          ? 0
                                                          : TestDelegate?.CursorLeft ?? 0, //Console.CursorLeft,
                                                top: Console.IsOutputRedirected
                                                         ? 0
                                                         : TestDelegate?.CursorTop ?? Console.CursorTop,
                                                width: TestDelegate?.IsOutputRedirected ?? Console.IsOutputRedirected
                                                           ? 100
                                                           : TestDelegate?.Width ?? Console.WindowWidth,
                                                height: int.MaxValue,
                                                isOverwrittenOnRender: false)
        {
        }

        //public override int Height => int.MaxValue;

        //public override int Width => TestDelegate?.IsOutputRedirected ?? Console.IsOutputRedirected
        //                                 ? 100
        //                                 : base.Width;

        //public override int Top => TestDelegate?.IsOutputRedirected ?? Console.IsOutputRedirected
        //                               ? 0
        //                               : base.Top;

        //public override int Left => Console.IsOutputRedirected
        //                                ? 0
        //                                : TestDelegate?.CursorLeft ?? Console.CursorLeft;
    }
}