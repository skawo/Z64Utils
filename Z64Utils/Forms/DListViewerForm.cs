﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL;
using OpenTK.Platform;
using OpenTK;
using RDP;
using System.IO;
using System.Globalization;
using Syroot.BinaryData;
using Z64;
using Common;

namespace Z64.Forms
{
    public partial class DListViewerForm : MicrosoftFontForm
    {
        class RenderRoutine
        {
            public uint Address;
            public int X;
            public int Y;
            public int Z;
            public F3DZEX.Dlist Dlist;

            public RenderRoutine(uint addr, int x = 0, int y = 0, int z = 0)
            {
                Address = addr;
                X = x;
                Y = y;
                Z = z;
                Dlist = null;
            }
            public RenderRoutine(F3DZEX.Render.Renderer renderer, uint addr, int x = 0, int y = 0, int z = 0) : this(addr, x, y, z)
            {
                Dlist = renderer.GetDlist(addr);
            }

            public override string ToString() => $"{Address:X8} [{X};{Y};{Z}]";
        }

        public static DListViewerForm Instance { get; set; }

        Z64Game _game;
        F3DZEX.Render.Renderer _renderer;
        SegmentEditorForm _segForm;
        DisasmForm _disasForm;
        RenderSettingsForm _settingsForm;
        F3DZEX.Render.Renderer.Config _rendererCfg;

        List<RenderRoutine> _routines;


        private DListViewerForm(Z64Game game)
        {
            _game = game;
            _rendererCfg = new F3DZEX.Render.Renderer.Config();

            InitializeComponent();
            Toolkit.Init();

            _renderer = new F3DZEX.Render.Renderer(game, _rendererCfg);
            modelViewer.RenderCallback = RenderCallback;

            _routines = new List<RenderRoutine>();
            SetupDLists();
            NewRender();
        }

        void SetupDLists()
        {
            _renderer.ClearErrors();
            foreach (RenderRoutine routine in _routines)
                routine.Dlist = _renderer.GetDlist(routine.Address);
        }

        void RenderCallback(Matrix4 proj, Matrix4 view)
        {
            _renderer.RenderStart(proj, view);
            foreach (var routine in _routines)
            {
                _renderer.RenderDList(routine.Dlist);
            }

            toolStripStatusErrorLabel.Text = _renderer.RenderFailed()
                ? $"RENDER ERROR AT 0x{_renderer.RenderErrorAddr:X8}! ({_renderer.ErrorMsg})"
                : "";
        }
        private void NewRender(object sender = null, EventArgs e = null)
        {
            _renderer.ClearErrors();

            toolStripStatusErrorLabel.Text = "";

            modelViewer.Render();
        }





        public static void OpenInstance(Z64Game game)
        {
            if (Instance == null)
            {
                Instance = new DListViewerForm(game);
                Instance.Show();
            }
            else
            {
                Instance.Activate();
            }
        }
        
        public void SetSegment(int index, F3DZEX.Memory.Segment segment)
        {
            if (index >= 0 && index < F3DZEX.Memory.Segment.COUNT)
            {
                _renderer.Memory.Segments[index] = segment;
                SetupDLists();
                NewRender();
            }
        }

        public void SetSingleDlist(uint vaddr)
        {
            listBox_routines.Items.Clear();
            _routines.Clear();

            var routine = new RenderRoutine(_renderer, vaddr);
            listBox_routines.Items.Add(routine.ToString());
            _routines.Add(routine);

            NewRender();
        }

        public void AddDList(uint vaddr)
        {
            var routine = new RenderRoutine(_renderer, vaddr);
            listBox_routines.Items.Add(routine.ToString());
            _routines.Add(routine);

            NewRender();
        }








        private void toolStripTextBoxEntrypoint_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                modelViewer.Focus();
            }
        }

        private void toolStripSegmentsBtn_Click(object sender, EventArgs e)
        {
            if (_segForm != null)
            {
                _segForm.Activate();
            }
            else
            {
                _segForm = new SegmentEditorForm(_game, _renderer);
                _segForm.SegmentsChanged += SegForm_SegmentsChanged;
                _segForm.FormClosed += (sender, e) => _segForm = null;
                _segForm.Show();
            }
        }

        private void SegForm_SegmentsChanged(object sender, F3DZEX.Memory.Segment e)
        {
            _renderer.Memory.Segments[(int)sender] = e;
            NewRender();
        }

        private void toolStripDisasBtn_Click(object sender, EventArgs e)
        {
            if (_disasForm != null)
            {
                _disasForm.Activate();
            }
            else
            {
                _disasForm = new DisasmForm(defaultText: "No Dlist selected");

                _disasForm.FormClosed += (sender, e) => _disasForm = null;
                _disasForm.Show();
            }

            if (listBox_routines.SelectedIndex != -1)
                _disasForm.UpdateDlist(_routines[listBox_routines.SelectedIndex].Dlist);
        }


        private void DListViewerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Instance = null;
            _disasForm?.Close();
            _segForm?.Close();
            _settingsForm?.Close();
        }

        private void toolStripRenderCfgBtn_Click(object sender, EventArgs e)
        {
            if (_settingsForm != null)
            {
                _settingsForm.Activate();
            }
            else
            {
                _settingsForm = new RenderSettingsForm(_rendererCfg);
                _settingsForm.FormClosed += (sender, e) => _settingsForm = null;
                _settingsForm.SettingsChanged += (sender, e) => { modelViewer.Render(); };
                _settingsForm.Show();
            }
        }

        private void saveScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.FileName = "";
            saveFileDialog1.Filter = Filters.PNG;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                var bmp = modelViewer.CaptureScreen();
                bmp.Save(saveFileDialog1.FileName);
            }
        }

        private void copyToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var bmp = modelViewer.CaptureScreen();
            Clipboard.SetImage(bmp);
        }

        private void modelViewer_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(modelViewer.PointToScreen(e.Location));
            }
        }

        private void listBox_routines_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = listBox_routines.SelectedIndex;
            if (idx >= 0 && idx < _routines.Count)
                _disasForm?.UpdateDlist(_routines[idx].Dlist);
        }

        private void modelViewer_Load(object sender, EventArgs e)
        {

        }
    }
}
