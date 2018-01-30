﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WLEditor
{
	public static class Level
	{
		public static Color[] paletteColors;
		public static Color[][] palettes =
		{
			new[] { Color.FromArgb(255, 224, 248, 208), Color.FromArgb(255, 135, 201, 140), Color.FromArgb(255, 52, 104, 86), Color.FromArgb(255, 8, 24, 32) },
			new[] { Color.White, Color.FromArgb(255, 170, 170, 170), Color.FromArgb(255, 85, 85, 85), Color.Black },
			new[] { Color.White, Color.FromArgb(255, 230, 214, 156), Color.FromArgb(255, 180, 165, 106), Color.FromArgb(255, 57, 56, 41) }
		};

		public static string[] levelNames =
		{
			"SS Teacup 1",
			"Parsley Woods 3",
			"Sherbet Land 2",
			"Stove Canyon 1",
			"Sherbet Land 3",
			"Mt. Teapot 4",
			"Mt. Teapot 1",
			"Rice Beach 1",
			"Sherbet Land 4",
			"Mt. Teapot 6",
			"Mt. Teapot 7",
			"SS Teacup 4",
			"Rice Beach 4",
			"Mt. Teapot 3",
			"Rice Beach 3",
			"Rice Beach 2",
			"Mt. Teapot 2",
			"Mt. Teapot 5",
			"Parsley Woods 5",
			"Parsley Woods 4",
			"SS Teacup 5",
			"Stove Canyon 2",
			"Stove Canyon 3",
			"Rice Beach 1 - FLOODED",
			"Sherbet Land 6",
			"Rice Beach 5",
			"Parsley Woods 6",
			"Stove Canyon 5",
			"Stove Canyon 6",
			"Parsley Woods 2",
			"SS Teacup 2",
			"SS Teacup 3",
			"Sherbet Land 5",
			"Sherbet Land 1",
			"Syrup Castle 2",
			"Syrup Castle 3",
			"Rice Beach 3 - FLOODED",
			"Syrup Castle 1",
			"Parsley Woods 1 - FLOODED",
			"Stove Canyon 4",
			"Syrup Castle 4",
			"Rice Beach 6",
			"Parsley Woods 1 - DRAINED"
		};

		public static byte[] levelData;
		public static byte[] objectsData;
		public static byte[] scrollData;
		public static byte[] warps;
		public static int warioPosition;

		public static void DumpLevel(Rom rom, int course, int warp, DirectBitmap tiles8x8, DirectBitmap tiles16x16, bool reloadAll, bool switchA, bool switchB, int paletteIndex)
		{
			rom.SetBank(0xC);
			int header = rom.ReadWord(0x4560 + course * 2);

			int tilebank = rom.ReadByte(header + 0);
			int tileaddressB = rom.ReadWord(header + 1);
			int tileaddressA = rom.ReadWord(header + 3);
			int tileanimated = rom.ReadWord(header + 7);

			int blockbank = rom.ReadByte(header + 9);
			int blockubbank = rom.ReadByte(header + 10);
			int blockindex = rom.ReadWord(header + 11);
			byte palette = rom.ReadByte(header + 27);

			//load warp
			if(warp != -1)
			{
				tilebank = rom.ReadByte(warp + 11);
				tileaddressB = rom.ReadWord(warp + 12);
				tileaddressA = rom.ReadWord(warp + 14);
				tileanimated = rom.ReadWord(warp + 18);
				blockindex = rom.ReadWord(warp + 20);
				palette = rom.ReadByte(warp + 10);
			}

			//set palette
			paletteColors = palettes[paletteIndex];

			//dump 8x8 blocks
			rom.SetBank(0x11);
			Dump8x8Tiles(rom, tileaddressA, tiles8x8, 2*16, 0, palette);
			rom.SetBank(tilebank);
			Dump8x8Tiles(rom, tileaddressB, tiles8x8, 6*16, 2, palette);
			rom.SetBank(0x11);
			Dump8x8Tiles(rom, tileanimated, tiles8x8, 4, 2, palette);

			//dump 16x16 blocks
			rom.SetBank(0xB);
			Dump16x16Tiles(rom, blockindex, tiles8x8, tiles16x16, switchA, switchB);

			if(reloadAll)
			{
				//dump level
				rom.SetBank(blockbank);
				int tilesdata = rom.ReadWord(0x4000 + blockubbank * 2);
				levelData = RLEDecompressTiles(rom, tilesdata).Take(0x3000).ToArray();

				//dump scroll
				rom.SetBank(0xC);
				scrollData = rom.ReadBytes(0x4000 + course * 32, 32);

				//dump objects
				rom.SetBank(0x7);
				int objectsPosition = rom.ReadWord(0x4199 + course * 2);
				objectsData = RLEDecompressObjects(rom, objectsPosition).Take(0x2000).ToArray();

				//dump warps
				rom.SetBank(0xC);
				warps = new Byte[32];
				for(int i = 0 ; i < 32 ; i++)
				{
					int warpdata = rom.ReadWord(0x4F30 + course * 64 + i * 2);
					warps[i] = rom.ReadByte(warpdata);
				}

				warioPosition = rom.ReadWordSwap(header + 15) + rom.ReadWordSwap(header + 13) * 8192;
			}
		}

		public static int SearchWarp(Rom rom, int course, int sector)
		{
			rom.SetBank(0xC);
			int warp = rom.ReadWord(0x4F30 + course * 64 + sector * 2);
			if(rom.ReadByte(warp) != 32)
			{
				//search sector that target that sector
				for(int i = 0 ; i < 32 ; i++)
				{
					warp = rom.ReadWord(0x4F30 + course * 64 + i * 2);

					if(rom.ReadByte(warp) == sector)
					{
						return warp;
					}
				}
			}

			return -1;
		}

		public static IEnumerable<byte> RLEDecompressTiles(Rom rom, int tilesdata)
		{
			while(true)
			{
				byte data = rom.ReadByte(tilesdata++);
				if((data & 0x80) == 0x80)
				{
					data = (byte)(data & 0x7F);
					byte rep = rom.ReadByte(tilesdata++);
					for(int j = 0 ; j <= rep ; j++)
					{
						yield return data;
					}
				}
				else
				{
					yield return data;
				}
			}
		}

		public static IEnumerable<byte> RLECompressTiles(byte[] levelData)
		{
			int current = 0;
			while(current < levelData.Length)
			{
				byte repeat = 0;
				byte data = levelData[current++];
				while(current < levelData.Length && levelData[current] == data && repeat < 255 && current % 256 != 0)
				{
					current++;
					repeat++;
				}

				if(repeat > 0)
				{
					yield return (byte)(0x80 | data);
					yield return repeat;
				}
				else
				{
					yield return data;
				}
			}
		}

		public static IEnumerable<byte> RLEDecompressObjects(Rom rom, int enemiesData)
		{
			while(true)
			{
				byte data = rom.ReadByte(enemiesData++);
				yield return (byte)(data >> 4);
				yield return (byte)(data & 0xF);

				byte repeat = rom.ReadByte(enemiesData++);
				for(int i = 0 ; i < repeat ; i++)
				{
					yield return 0;
					yield return 0;
				}
			}
		}

		public static IEnumerable<byte> RLECompressObjectsHelper(byte[] levelData)
		{
			for(int i = 0 ; i < levelData.Length ; i+=2)
			{
				yield return (byte)((levelData[i] << 4) | levelData[i+1]);
			}
		}

		public static IEnumerable<byte> RLECompressObjects(byte[] levelData)
		{
			byte[] halfData = RLECompressObjectsHelper(levelData).ToArray();

			int current = 0;
			while(current < halfData.Length)
			{
				byte data = halfData[current++];
				yield return data;

				byte count = 0;
				while(current < halfData.Length && halfData[current] == 0 && count < 255)
				{
					current++;
					count++;
				}
				yield return count;
			}
		}

		public static void Dump16x16Tiles(Rom rom, int tileindexaddress, DirectBitmap gfx8, DirectBitmap gfx16, bool switchA, bool switchB)
		{	
			for(int n = 0 ; n < 16 ; n++)
			{
				for(int i = 0 ; i < 8 ; i++)
				{
					int tileIndex = i + n * 8;
					tileIndex = Level.Switch(tileIndex, switchA, switchB);

					for(int k = 0 ; k < 2 ; k++)
					{
						for(int j = 0 ; j < 2 ; j++)
						{
							byte subTileIndex = rom.ReadByte(tileindexaddress + tileIndex * 4 + k * 2 + j);
							Point dest = new Point(j * 8 + i * 16, k * 8 + n * 16);

							if(subTileIndex < 128)
							{
								Point source = new Point((subTileIndex % 16) * 8, (subTileIndex / 16) * 8);
															
								for(int y = 0 ; y < 8 ; y++)
								{
									Array.Copy(gfx8.Bits, source.X + (source.Y + y) * gfx8.Width, gfx16.Bits, dest.X + (dest.Y + y) * gfx16.Width, 8);
								}
							}
							else
							{
								int defaultColor = paletteColors[0].ToArgb();								
								for(int y = 0 ; y < 8 ; y++)
								{
									int destIndex = dest.X + (dest.Y + y) * gfx16.Width;									
									for(int x = 0 ; x < 8 ; x++)
									{
										gfx16.Bits[destIndex + x] = defaultColor;
									}
								}
							}
						}
					}
				}
			}
		}

		public static void Dump8x8Tiles(Rom rom, int gfxaddress, DirectBitmap gfx8, int tiles, int rowpos, byte palette)
		{
			for(int n = 0 ; n < tiles ; n++)
			{
				for(int j = 0 ; j < 8 ; j++)
				{
					byte data0 = rom.ReadByte(gfxaddress++);
					byte data1 = rom.ReadByte(gfxaddress++);

					for(int i = 0 ; i < 8 ; i++)
					{
						int pixelA = (data0 >> (7 - i)) & 0x1;
						int pixelB = (data1 >> (7 - i)) & 0x1;
						int pixel = pixelA + pixelB * 2;
						int palindex = (palette >> pixel * 2) & 0x3;

						int x = i + n%16 * 8;
						int y = j + (n/16 + rowpos) * 8;

						gfx8.Bits[x + gfx8.Width * y] = paletteColors[palindex].ToArgb();
					}
				}
			}
		}

		public static bool IsCollidable(int tileIndex)
		{
			return (tileIndex & 64) != 64;
		}

		public static bool IsDoor(int tileIndex)
		{
			return tileIndex == 72 || tileIndex == 75 || tileIndex == 46 || tileIndex == 84;
		}

		//replace tiles when a (!) block is hit
		public static int Switch(int tileData, bool switchA, bool switchB)
		{
			if(switchA)
			{
				switch(tileData)
				{
					case 0x7C:
						return 0x27;

					case 0x27:
						return 0x7C;

					case 0x7A:
						return 0x44;

					case 0x79:
						return 0x45;
				}
			}
			else if(switchB)
			{
				switch(tileData)
				{
					case 0x7C:
						return 0x55;

					case 0x55:
						return 0x7C;

					case 0x7A:
						return 0x7B;

					case 0x7B:
						return 0x7A;
						
					case 0x59:
						return 0x5D;

					case 0x5D:
						return 0x59;
				}
			}

			return tileData;
		}

		public static bool SaveChanges(Rom rom, int course, string filePath, out string errorMessage)
		{
			//rom expansion give new banks for level data
			rom.ExpandTo1MB();

			SaveBlocksToRom(rom, course);
			if(!SaveObjectsToRom(rom, course, out errorMessage))
			{
				return false;
			}

			rom.FixCRC();
			rom.Save(filePath);
			return true;
		}

		public static void SaveBlocksToRom(Rom rom, int course)
		{
			//grab all levels data at once
			byte[][] allTiles = new byte[0x2B][];

			for(int i = 0 ; i < 0x2B ; i++)
			{
				rom.SetBank(0xC);
				int headerposition = rom.ReadWord(0x4560 + i * 2);
				int tilebank = rom.ReadByte(headerposition + 9);
				int tilesubbank = rom.ReadByte(headerposition + 10);

				rom.SetBank(tilebank);
				int tilePosition = rom.ReadWord(0x4000 + tilesubbank * 2);
				byte[] tileData = RLEDecompressTiles(rom, tilePosition).Take(0x3000).ToArray();
				allTiles[i] = tileData;
			}

			Array.Copy(levelData, allTiles[course], 0x3000);

			//write them back to ROM
			byte bank = 0x20;
			byte subbank = 0;
			int writePosition = 0x4040; //first 64 bytes are reserved for level pointers
			for(int i = 0 ; i < 0x2B ; i++)
			{
				byte[] tileData = RLECompressTiles(allTiles[i]).ToArray();
				if((writePosition + tileData.Length) >= 0x8000 || subbank >= 32)
				{
					//no more space, switch to another bank
					bank++;
					subbank = 0;
					writePosition = 0x4040;
				}

				//write data to bank
				rom.SetBank(bank);
				rom.WriteWord(0x4000 + subbank * 2, (ushort)writePosition);
				rom.WriteBytes(writePosition, tileData);

				//update level header
				rom.SetBank(0xC);
				int headerposition = rom.ReadWord(0x4560 + i * 2);
				rom.WriteByte(headerposition + 9, bank);
				rom.WriteByte(headerposition + 10, subbank);

				subbank++;
				writePosition += tileData.Length;
			}
		}

		public static bool SaveObjectsToRom(Rom rom, int course, out string errorMessage)
		{
			byte[][] enemyData = new byte[0x2B][];

			//save objects
			rom.SetBank(0x7);
			for(int i = 0 ; i < 0x2B ; i++)
			{
				int objectsPos = rom.ReadWord(0x4199 + i * 2);
				enemyData[i] = RLEDecompressObjects(rom, objectsPos).Take(0x2000).ToArray();
			}
			enemyData[course]= objectsData;

			int objectSize = enemyData.Sum(x => RLECompressObjects(x).Count());
			if(objectSize > 4198)
			{
				errorMessage = string.Format("Object data is too big to fit in ROM.\r\nPlease remove at least {0} byte(s).", objectSize - 4198);
				return false;
			}

			int startPos = rom.ReadWord(0x4199);
			for(int i = 0 ; i < 0x2B ; i++)
			{
				rom.WriteWord(0x4199 + i * 2, (ushort)startPos);

				byte[] data = RLECompressObjects(enemyData[i]).ToArray();
				rom.WriteBytes(startPos, data);
				startPos += data.Length;
			}

			errorMessage = string.Empty;
			return true;
		}
	}
}
