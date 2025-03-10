﻿#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Antlr4.Runtime;
using NINA.Photon.Plugin.ASA.Exceptions;
using System.IO;

namespace NINA.Photon.Plugin.ASA.Utility
{
    public class ThrowingErrorListener : BaseErrorListener
    {
        public static readonly ThrowingErrorListener INSTANCE = new ThrowingErrorListener();

        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            throw new ParseCancellationException($"line {line}:{charPositionInLine} {msg}");
        }
    }
}