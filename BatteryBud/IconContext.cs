﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BatteryBud
{
  // TODO: make localization and delete this line
  [SuppressMessage("ReSharper", "LocalizableElement")]
  public class IconContext : ApplicationContext
  {
    private const int UPDATE_INTERVAL = 1000; //ms

    private readonly int[ ] _digitSep = new int[10];

    private readonly MenuItem _itemAdd;

    private readonly MenuItem _itemRemove;
    private readonly PowerStatus _pow = SystemInformation.PowerStatus;

    private readonly string _saveFileName = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                            "\\Battery Bud\\save.sav";

    private readonly Timer _timer = new Timer( );

    private readonly NotifyIcon _trayIcon = new NotifyIcon( );

/*
    private string _autostartLinkLocation = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                            "\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\BatteryBud.lnk";
*/

    private Image _digits;

    private int _digitWidth;

    private int
      _percentagePrev = -1,
      _percentageCurrent = -1;

    /// <summary>
    ///   Initializing stuff
    /// </summary>
    public IconContext( )
    {
      if (_pow.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery)
      {
        ShowNoBatteryError( );
        return;
      }
      InitDigits( );

      UpdateBattery(null, null);

      // Context menu.
      _itemAdd = new MenuItem("Add to autostart.", SetAutostart);
      _itemRemove = new MenuItem("Remove from autostart.", ResetAutostart);

      MenuItem[ ] autostart = { _itemAdd, _itemRemove };

      _trayIcon.ContextMenu = new ContextMenu(new[ ]
      {
        new MenuItem("About", About),
        new MenuItem("Autostart", autostart),
        new MenuItem("Close", Close)
      });

      _trayIcon.Visible = true;

      // Loading autostart info.
      try
      {
        FileStream file = File.OpenRead(_saveFileName);
        char autostartEnabled = (char) file.ReadByte( );
        file.Close( );
        if (autostartEnabled == '1')
        {
          SetAutostart(null, null);
        }
        else
        {
          ResetAutostart(null, null);
        }
      }
      catch (FileNotFoundException) // Happens when some idiot deletes save file.
      {
        SetAutostart(null, null);
      }
      catch (DirectoryNotFoundException) // Happens on first launch. 
      {
        Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                  "\\Battery Bud");
        SetAutostart(null, null);
      }

      // Timer
      _timer.Interval = UPDATE_INTERVAL;
      _timer.Tick += UpdateBattery;
      _timer.Enabled = true;
    }


    public void ShowNoBatteryError( )
    {
      // If a user tries to run program from computer with no battery to track... this is stupid. And sad.
      MessageBox.Show("You're trying to run Battery Bud from desktop PC. What were you thinking? :|", "wut",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
      Application.ExitThread( );
      Environment.Exit(1);
    }

    /// <summary>
    ///   Main update event handler
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="e">Event arguments</param>
    private void UpdateBattery(object sender, EventArgs e)
    {
      _percentageCurrent = (int) Math.Round(_pow.BatteryLifePercent * 100.0);

      if (_percentagePrev != _percentageCurrent)
      {
        //Updating icon.
        _trayIcon.Icon?.Dispose( );

        Image image = RenderIcon(_percentageCurrent);
        _trayIcon.Icon = ToIcon(image);
        image.Dispose( );
      }

      _percentagePrev = _percentageCurrent;
    }

    /// <summary>
    ///   About onClick handler
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="e">Event arguments</param>
    private static void About(object sender, EventArgs e)
    {
      MessageBox.Show("Battery Bud v" + Program.Version + " by gn.fur.\n"
                      + "Thanks to Konstantin Luzgin and Hans Passant."
                      + "\nContact: foxoftgames@gmail.com", "About");
    }

    private void Close(object sender, EventArgs e)
    {
      _trayIcon.Visible = false;
      Application.ExitThread( );
      Application.Exit( );
    }

    /// <summary>
    ///   Checks registry. If there's no autostart key or it's defferent, sets it to proper value.
    ///   Also writes 1 to savefile.
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="args">Event arguments</param>
    private void SetAutostart(object sender, EventArgs args)
    {
      _itemAdd.Checked = true;
      _itemRemove.Checked = false;

      RegistryKey rkApp =
        Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
      if (rkApp != null)
      {
        string regVal = (string) rkApp.GetValue("BatteryBud");

        if (regVal == null || !Application.ExecutablePath.Equals(regVal, StringComparison.OrdinalIgnoreCase))
        {
          try
          {
            rkApp.SetValue("BatteryBud", Application.ExecutablePath);
          }
          catch (Exception)
          {
            // ignored
          }
        }
        rkApp.Close( );
      }

      FileStream file = File.OpenWrite(_saveFileName);
      file.WriteByte((byte) '1');
      file.Close( );
    }

    /// <summary>
    ///   Deletes registry key and writes 0 to savefile
    /// </summary>
    /// <param name="sender">Sender of the event</param>
    /// <param name="args">Event arguments</param>
    private void ResetAutostart(object sender, EventArgs args)
    {
      _itemAdd.Checked = false;
      _itemRemove.Checked = true;

      RegistryKey rkApp =
        Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

      if (rkApp != null)
      {
        if (rkApp.GetValue("BatteryBud") != null)
        {
          try
          {
            rkApp.DeleteValue("BatteryBud");
          }
          catch (Exception)
          {
            // ignored
          }
        }
        rkApp.Close( );
      }

      FileStream file = File.OpenWrite(_saveFileName);
      file.WriteByte((byte) '0');
      file.Close( );
    }

    /// <summary>
    ///   Renders icon using loaded font.
    ///   Render works from right to left.
    /// </summary>
    /// <param name="numberToRender">Number to render</param>
    /// <returns>Rendered icon</returns>
    public Image RenderIcon(int numberToRender)
    {
      int number = numberToRender;

      int x = 16;
      Image image = new Bitmap(16, 16);

      using (Graphics surf = Graphics.FromImage(image))
      {
        while (number != 0)
        {
          int digit = number % 10; // Getting last digit.
          number = (number - digit) / 10;

          int xadd = _digitWidth - _digitSep[digit];
          x -= xadd;

          surf.DrawImage(_digits, x, 0,
            new Rectangle(digit * _digitWidth + _digitSep[digit], 0, xadd, 16),
            GraphicsUnit.Pixel); //Some sick math here. : - )
        }
      }
      return image;
    }

    /// <summary>
    ///   Loads font file and measures digit's width
    /// </summary>
    public void InitDigits( )
    {
      try
      {
        _digits = Image.FromFile(AppDomain.CurrentDomain.BaseDirectory + "res\\digits.png");
      }
      catch (FileNotFoundException)
      {
        ShowNoBatteryError( );
        return;
      }

      _digitWidth = (int) Math.Round(_digits.Width / 10f);

      // Measuring each digit width.
      Bitmap imgBuf = new Bitmap(_digits);
      try
      {
        for (int i = 0; i < 10; i += 1)
        {
          int baseX = i * _digitWidth;
          bool found = false;
          for (int x = 0; x < _digitWidth; x += 1)
          {
            for (int y = 0; y < _digits.Height; y += 1)
            {
              if (imgBuf.GetPixel(baseX + x, y).A == 0)
              {
                continue;
              }
              found = true;
              break;
            }

            if (!found)
            {
              continue;
            }
            _digitSep[i] = x;
            break;
          }
        }
      }
      catch (ArgumentOutOfRangeException) // For retards who will try to give microscopic images to the program.
      {
        ShowNoBatteryError( );
      }

      imgBuf.Dispose( );
    }

    /// <summary>
    ///   * Converts Image to Icon using magic I don't really care about at this point.
    ///   * Standart conversion messes up with transparency. Not cool, Microsoft, not cool.
    ///   * Author: Hans Passant
    ///   * https://stackoverflow.com/questions/21387391/how-to-convert-an-image-to-an-icon-without-losing-transparency
    /// </summary>
    /// <param name="image">Image to convert</param>
    /// <returns>Converted icon</returns>
    public Icon ToIcon(Image image)
    {
      MemoryStream ms = new MemoryStream( );
      BinaryWriter bw = new BinaryWriter(ms);
      // Header
      bw.Write((short) 0); // 0 : reserved
      bw.Write((short) 1); // 2 : 1=ico, 2=cur
      bw.Write((short) 1); // 4 : number of images
      // Image directory
      int w = image.Width;
      if (w >= 256)
      {
        w = 0;
      }
      bw.Write((byte) w); // 0 : width of image
      int h = image.Height;
      if (h >= 256)
      {
        h = 0;
      }
      bw.Write((byte) h); // 1 : height of image
      bw.Write((byte) 0); // 2 : number of colors in palette
      bw.Write((byte) 0); // 3 : reserved
      bw.Write((short) 0); // 4 : number of color planes
      bw.Write((short) 0); // 6 : bits per pixel
      long sizeHere = ms.Position;
      bw.Write(0); // 8 : image size
      int start = (int) ms.Position + 4;
      bw.Write(start); // 12: offset of image data
      // Image data
      image.Save(ms, ImageFormat.Png);
      int imageSize = (int) ms.Position - start;
      ms.Seek(sizeHere, SeekOrigin.Begin);
      bw.Write(imageSize);
      ms.Seek(0, SeekOrigin.Begin);

      // And load it
      return new Icon(ms);
    }
  }
}