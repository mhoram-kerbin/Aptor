/**
 * Utility.cs - Utility functions for Aptor
 *
 * This file is part of Aptor.
 *
 * Copyright 2014 Mhoram Kerbin
 *
 * Aptor is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Aptor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Aptor. If not, see <http://www.gnu.org/licenses/>.
 *
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Aptor
{
    static class Utility
    {
        static public List<Part> getPartList()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return EditorLogic.SortedShipList;
            }
            else
            {
                throw new NotImplementedException("AD else branch of getPartList");
            }
        }

    }
}
