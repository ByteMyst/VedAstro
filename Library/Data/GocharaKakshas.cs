﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;

namespace VedAstro.Library
{
    /// <summary>
    /// Data representation of Gochara Kakshas
    /// </summary>
    public class GocharaKakshas : IToJson
    {
        public GocharaKakshas(List<PlanetName> column1, Dictionary<PlanetName, ZodiacSign> column2, Dictionary<PlanetName, string> column3, Dictionary<PlanetName, int> column4, Dictionary<PlanetName, int> column5, Dictionary<PlanetName, int> column6)
        {
            Column1 = column1;
            Column2 = column2;
            Column3 = column3;
            Column4 = column4;
            Column5 = column5;
            Column6 = column6;
        }

        public List<PlanetName> Column1 { get; }
        public Dictionary<PlanetName, ZodiacSign> Column2 { get; }
        public Dictionary<PlanetName, string> Column3 { get; }
        public Dictionary<PlanetName, int> Column4 { get; }
        public Dictionary<PlanetName, int> Column5 { get; }
        public Dictionary<PlanetName, int> Column6 { get; }

        public JObject ToJson()
        {
            //put together final table for caller
            var holder = new JObject();
            foreach (var mainPlanet in Column1)
            {
                //package the row
                var valueHolder = new JObject
                {
                    //make the columns
                    ["Planet"] = mainPlanet.Name.ToString(),
                    ["Sign"] = Column2[mainPlanet].GetSignName().ToString(),//current sign
                    ["KakshaScore"] = Column4[mainPlanet],
                    ["KakshaLord"] = Column3[mainPlanet],
                    ["Ashtaka"] = Column5[mainPlanet],
                    ["Sarvashtaka"] = Column6[mainPlanet],
                };

                holder[mainPlanet.Name.ToString()] = valueHolder;
            }

            return holder;

        }

    }

}
