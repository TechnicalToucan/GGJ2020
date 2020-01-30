﻿using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using EaserCore;
using System.Text.RegularExpressions;
using System.IO;

public class EaserEditorWindow : EditorWindow
{

	private static EaserDataWrapper _easer;
	private static EaserEditorWindow _window;

	#region Menu
	[MenuItem("Tools/Easer/Open Ease Editor", false, 0)]
	public static void Menu_OpenEaser()
	{
		Init();
	}

	[MenuItem("Tools/Easer/Generate Eases Enum", false, 1000)]
	public static void Menu_GenerateEnum()
	{
		GenerateEnums();
	}

	[MenuItem("Tools/Easer/Official Website", false, 2000)]
	[MenuItem("Help/Easer/Official Website", false, 2000)]
	public static void HelpWebsite()
	{
		Application.OpenURL("http://easer.tonycoculuzzi.com");
	}

	[MenuItem("Tools/Easer/Getting Started", false, 2000)]
	[MenuItem("Help/Easer/Getting Started", false, 2000)]
	public static void HelpDocs()
	{
		Application.OpenURL("http://easer.tonycoculuzzi.com/docs.php");
	}

	[MenuItem("Tools/Easer/Code Documentation", false, 2000)]
	[MenuItem("Help/Easer/Code Documentation", false, 2000)]
	public static void HelpCode()
	{
		Application.OpenURL("http://easer.tonycoculuzzi.com/code.php");
	}

	[MenuItem("Tools/Easer/Contact", false, 2000)]
	[MenuItem("Help/Easer/Contact", false, 2000)]
	public static void HelpContact()
	{
		Application.OpenURL("mailto:tonycoculuzzi@gmail.com");
	}
	#endregion

	#region EditorWindow
	static void Init()
	{
		_window = (EaserEditorWindow)EditorWindow.GetWindow(typeof(EaserEditorWindow));
		_window.initWindow();
		Load();
	}

	private void initWindow()
	{
		title = "Easer";
		minSize = new Vector2(682, 503);
	}

	public void OnGUI()
	{
		drawGUI();
		if (GUI.changed) { EditorUtility.SetDirty(_easer); }
	}
	#endregion

	#region Saving and Loading
	public static void New()
	{
		_easer = ScriptableObject.CreateInstance<EaserDataWrapper>();
		Save();
	}

	public static void Save()
	{
		if (_easer == null)
		{
			Debug.LogWarning("Easer data has not been loaded, not saving.");
			return;
		}

		// Check/Create directories
		string[] directories = Easer.DATA_PATH.Split('/');
		string currentPath = "";
		for (int i = 0; i < directories.Length; i++)
		{
			string dir = directories[i];
			string checkDir = Application.dataPath + "/" + currentPath + dir;
			string parentDir = "Assets" + ((i > 0) ? '/' + currentPath.Remove(currentPath.Length - 1) : "");
			if (!System.IO.Directory.Exists(checkDir)) { AssetDatabase.CreateFolder(parentDir, dir); }
			currentPath += dir + '/';
		}
		currentPath += Easer.DATA_FILENAME;

		if (!System.IO.File.Exists(Application.dataPath + "/" + currentPath))
		{
			AssetDatabase.CreateAsset(_easer, "Assets/" + currentPath);
		}

		EditorUtility.SetDirty(_easer);
		AssetDatabase.Refresh();
	}

	public static void Load()
	{
		_easer = Resources.Load<EaserDataWrapper>(Easer.DATA_FILENAME.Split('.')[0]);
		if (_easer == null)
		{
			Debug.LogWarning("Easer data does not exist, creating new.");
			New();
		}

		if (Selection.activeObject == null)
		{
			Selection.activeObject = _easer;
		}
	}
	#endregion

	#region Enum Generator
	public static void GenerateEnums()
	{
		if (_easer == null) { Load(); }

		// Check/Create directories
		string[] directories = Easer.ENUM_PATH.Split('/');
		string currentPath = "";
		for (int i = 0; i < directories.Length; i++)
		{
			string dir = directories[i];
			string checkDir = Application.dataPath + "/" + currentPath + dir;
			string parentDir = "Assets" + ((i > 0) ? '/' + currentPath.Remove(currentPath.Length - 1) : "");
			if (!System.IO.Directory.Exists(checkDir)) { AssetDatabase.CreateFolder(parentDir, dir); }
			currentPath += dir + '/';
		}

		List<string> lines = new List<string>();
		lines.Add("// This enum was generated by Easer");
		lines.Add("//\tDo not modify this file, it will be overwritten by Easer.");
		lines.Add("public enum EaserEase{");
		for (int i = 0; i < _easer.data.eases.Length; i += 1)
		{
			EaserEaseObject ease = _easer.data.eases[i];
			if (ease.name == "")
			{
				continue;
			}

			bool skip = false;

			for (int ii = 0; ii < i; ii++)
			{
				if (ease.name == _easer.data.eases[ii].name)
				{
					Debug.LogWarning("Ease " + i + ": '" + ease.name + "' has a name that is already in use. Skipping. Please rename this ease.");
					skip = true;
				}
			}

			if (skip) { continue; }

			string varName = ease.name.Replace(' ', '_');
			varName = Regex.Replace(varName, @"[^a-zA-Z0-9\_]+", "");

			lines.Add("\t" + varName + " = " + i + ",");
		}
		lines.Add("}");

		AssetDatabase.DeleteAsset(Easer.ENUM_PATH + '/' + "EaserEase.cs");
		File.WriteAllLines("Assets/" + Easer.ENUM_PATH + '/' + "EaserEase.cs", lines.ToArray());
		AssetDatabase.Refresh();

		// create with currentPath + ENUM_FILENAME
	}
	#endregion

	#region GUI
	private const int SELECTOR_WIDTH = 250;
	private const int CURVE_CANVAS_SIZE = 400;
	private float DOT_SIZE = 2;

	private Rect _selectorRect;
	private Rect _canvasRect;

	private Vector2 _scrollPos;
	private Vector2 _mousePos;
	private bool _mouseClick = false;
	//private bool _mouseDrag = false;
	private bool _clearGuiTarget = false;
	private bool _repaint;
	private Texture2D _texture;
	private float _dotValue;

	private float _lastTime;

	private void drawGUI()
	{
		//_data.test = EditorGUILayout.TextField(_data.test);

		setup();
		updateMouse();
		clearGuiTarget();

		_selectorRect = new Rect(0, 0, SELECTOR_WIDTH, Screen.height);
		_canvasRect = new Rect(SELECTOR_WIDTH, 0, Screen.width - SELECTOR_WIDTH, Screen.height);

		drawSelectorGUI();
		drawCanvasGUI();

		if (_repaint) {
			Repaint();
			_repaint = false;
		}
	}

	private void setup()
	{
		if (_easer == null) { Load(); }
		if (_easer.data == null) { _easer.data = new EaserData(); }
		if (_easer.data.eases == null) { _easer.data.eases = new EaserEaseObject[0]; }

	}

	private void updateMouse()
	{
		_mousePos = Event.current.mousePosition;
		_mouseClick = false;
		//_mouseDrag = false;

		if (Event.current.isMouse && Event.current.button == 0)
		{
			if (Event.current.type == EventType.MouseDown)
			{
				_clearGuiTarget = true;
				_mouseClick = true;
				_repaint = true;
			}
			else if (Event.current.type == EventType.MouseDrag)
			{
				//_mouseDrag = true;
			}
			else if (Event.current.type == EventType.MouseUp)
			{
				//_mouseDrag = false;
			}
		}
	}

	private void clearGuiTarget()
	{
		GUI.SetNextControlName("unfocus");
		EditorGUI.TextArea(new Rect(-100, -100, 1, 1), "");
		if (_clearGuiTarget) GUI.FocusControl("unfocus");
		_clearGuiTarget = false;
	}

	private void drawSelectorGUI()
	{
		Rect outerRect = new Rect(_selectorRect.x + 5, _selectorRect.y + 5, _selectorRect.width - 5, _selectorRect.height - 33);
		EaserEditorUtils.DrawOutsetBox(outerRect);
		Rect titleRect = EaserEditorUtils.TitleText(outerRect, "Ease Selector");

		Rect buttonsRect = new Rect(titleRect.x, titleRect.yMax + 5, titleRect.width, 35);
		EaserEditorUtils.DrawOutsetBox(buttonsRect);

		Rect addButtonRect = new Rect(buttonsRect.x + 5, buttonsRect.y + 5, buttonsRect.width * 0.5f - 5, buttonsRect.height - 10);
		if (GUI.Button(addButtonRect, "Add")) { Add(); }

		if (_easer.data.eases.Length < 1 || _easer.current < 0) { GUI.enabled = false; }
		Rect removeButtonRect = new Rect(buttonsRect.x + buttonsRect.width*0.5f + 5, buttonsRect.y + 5, buttonsRect.width * 0.5f - 10, buttonsRect.height - 10);
		if (GUI.Button(removeButtonRect, "Remove")) { Remove(); }
		GUI.enabled = true;

		Rect easesRect = new Rect(buttonsRect.x, buttonsRect.yMax + 5, buttonsRect.width, outerRect.height - (buttonsRect.yMax + 5));
		if (_mouseClick && easesRect.Contains(_mousePos)){
			_easer.current = -1;
			_repaint = true;
		}
		EaserEditorUtils.DrawInsetBox(easesRect);
			
		Rect scrollRect = EaserEditorUtils.InsetRect(easesRect, 1);
		Rect scrollContentRect = new Rect(0, 0, 10, 25 * _easer.data.eases.Length);
		int rectHeight = (int)scrollRect.height;
		int totalHeight = (25 * _easer.data.eases.Length);
		scrollContentRect.height = (rectHeight > totalHeight) ? rectHeight : totalHeight;
		_scrollPos = GUI.BeginScrollView(scrollRect, _scrollPos, scrollContentRect, false, true);

		for (int i = 0; i < _easer.data.eases.Length; i++)
		{
			Rect easeRect = new Rect(0, 25 * i, scrollRect.width - 15, 25);
			Rect boxRect = EaserEditorUtils.InsetRect(easeRect, 1);
			GUI.color = new Color(1, 1, 1, 0.5f);
			GUI.Box(boxRect, "");
			GUI.color = new Color(1, 1, 1, 0.4f);
			if (_easer.current == i)
			{
				EaserEditorUtils.DrawHighlightBox(boxRect);
				GUI.color = Color.white;
			}
			Rect easeNumberRect = new Rect(boxRect.x, boxRect.y, boxRect.width, boxRect.height);
			easeNumberRect.xMin += 4;
			easeNumberRect.yMin += 3;
			GUI.Label(easeNumberRect, i.ToString());
			Rect easeNameRect = easeNumberRect;
			easeNameRect.xMin += 25;
			string easeNameText = (_easer.data.eases[i].name == "") ? "-" : _easer.data.eases[i].name;
			GUI.Label(easeNameRect, easeNameText);
			GUI.color = GUI.contentColor;

			Vector2 mousePos = Event.current.mousePosition;
			if (easeRect.Contains(mousePos))
			{
				if(EditorGUIUtility.isProSkin){
					EaserEditorUtils.DrawHighlightBox(boxRect);
				}else{
					EaserEditorUtils.DrawOutlineBox(boxRect, new Color(0.4f,0,0,0.5f));
				}
				_repaint = true;
				if (_mouseClick)
				{
					_easer.current = i;
				}
			}
		}

		GUI.EndScrollView();
	}

	private void drawCanvasGUI()
	{
		Rect outerRect = new Rect(_canvasRect.x + 5, _canvasRect.y + 5, _canvasRect.width - 10, _canvasRect.height - 33);
		EaserEditorUtils.DrawOutsetBox(outerRect);
		Rect titleRect = EaserEditorUtils.TitleText(outerRect, "Ease Canvas");

		if (_easer.current < 0)
		{
			Rect helpRect = new Rect(titleRect.x, titleRect.yMax + 5, titleRect.width, 40);
			EditorGUI.HelpBox(helpRect, "No ease is selected, select an ease to edit!", MessageType.Info);
			return;
		}

		Rect toolbarRect = new Rect(titleRect.x, titleRect.yMax + 5, titleRect.width, 40);
		EaserEditorUtils.DrawOutsetBox(toolbarRect);

		Rect nameRect = EaserEditorUtils.InsetRect(toolbarRect, 5);
		nameRect.width = 300;
		EaserEditorUtils.DrawInsetBox(nameRect);
		nameRect = EaserEditorUtils.InsetRect(nameRect, 1);
		GUIStyle nameStyle = new GUIStyle(GUI.skin.textField);
		nameStyle.fontSize = 20;
		_easer.data.eases[_easer.current].name = GUI.TextField(nameRect, _easer.data.eases[_easer.current].name, nameStyle);

		Rect saveRect = nameRect;
		saveRect.xMax = toolbarRect.xMax - 5;
		saveRect.xMin = nameRect.xMax + 5;
		if (GUI.Button(saveRect, "Save"))
		{
			Save();
			GenerateEnums();
		}

		Rect curveContainerRect = new Rect(outerRect.x + 5, toolbarRect.yMax + 5, CURVE_CANVAS_SIZE + 12, CURVE_CANVAS_SIZE + 12);
		EaserEditorUtils.DrawOutsetBox(curveContainerRect);
		Rect curveRect = new Rect(curveContainerRect.x + 6, curveContainerRect.y + 6, CURVE_CANVAS_SIZE, CURVE_CANVAS_SIZE);
		EaserEditorUtils.DrawHighlightBox(new Rect(curveRect.x - 1, curveRect.y, curveRect.width + 2, curveRect.height + 1));
		_easer.data.eases[_easer.current].curve = EditorGUI.CurveField(curveRect, _easer.data.eases[_easer.current].curve, Color.red, new Rect(0,0,1,1));
		drawCurve(curveRect);
	}

	private void drawCurve(Rect rect)
	{
		if (_texture == null)
		{
			_texture = new Texture2D(1, 1);
			_texture.filterMode = FilterMode.Point;
			_texture.SetPixel(0, 0, new Color(1, 1, 1, 1));
			_texture.Apply();
		}
		if(EditorGUIUtility.isProSkin){
			GUI.color = new Color(0.2f, 0.2f, 0.2f, 1);
		}else{
			GUI.color = new Color(0.6f, 0.6f, 0.6f, 1);
		}

		Rect bgRect = rect;
		bgRect.yMin += 2;
		bgRect.xMin += 1;
		GUI.DrawTexture(bgRect, _texture);
		EaserEditorUtils.DrawOutlineBox(bgRect, Color.black);
		
		rect = EaserEditorUtils.InsetRect(rect, 50);
		if(EditorGUIUtility.isProSkin){
			EaserEditorUtils.DrawShadowBox(rect);
		}

		Color lineColor = new Color(0.15f, 0.15f, 0.15f, 1);
		Color offLineColor = new Color(0.15f, 0.15f, 0.15f, 0.3f);
		EaserEditorUtils.DrawGrid(rect, lineColor, offLineColor);

		AnimationCurve curve = _easer.data.eases[_easer.current].curve;
		//GUI.color = Color.red;
		//EditorGUIUtility.DrawCurveSwatch(rect, curve, null, Color.red, new Color(0, 0, 0, 0), new Rect(0, 0, 1, 1));
		//GUI.color = GUI.contentColor;

		float inc = 0.01f;
		for (float i = 0; i < 1; i += inc)
		{
			float val = curve.Evaluate(i);
			float next = curve.Evaluate(i + inc);

			Vector2 start = new Vector2(rect.x + (i * rect.width), (rect.y + rect.height) - (val * rect.height));
			Vector2 end = new Vector2(rect.x + ((i + inc) * rect.width), (rect.y + rect.height) - (next * rect.height));
			if(EditorGUIUtility.isProSkin){
				Handles.color = Color.red;
			}else{
				Handles.color = new Color(0.6f, 0, 0, 1);
			}
			Handles.DrawLine(start, end);
		}

		if (rect.Contains(_mousePos))
		{
			float deltaTime = Time.realtimeSinceStartup - _lastTime;
			float value = curve.Evaluate(_dotValue);
			_dotValue += deltaTime*0.5f;
			if (_dotValue > 1) _dotValue = 0;
			_lastTime = Time.realtimeSinceStartup;

			float x = rect.x + (_dotValue * rect.width);
			float y = (rect.y + rect.height) - (value * rect.height);
			Vector2 pos = new Vector2(x - (DOT_SIZE * 0.5f), y - (DOT_SIZE * 0.5f));
			//Vector2 xPos = new Vector2(x - (DOT_SIZE * 0.5f), (rect.yMax + 10) - (DOT_SIZE * 0.5f));
			Vector2 yPos = new Vector2((rect.xMax + 10) - (DOT_SIZE * 0.5f), y - (DOT_SIZE * 0.5f));

			//Handles.color = offLineColor;
			//Handles.DrawLine(new Vector2(x, y), new Vector3(x, xPos.y));
			//Handles.DrawLine(new Vector2(x, y), new Vector3(yPos.x, y));

			Handles.color = lineColor;
			Handles.DrawLine(new Vector2(rect.xMax + 10, rect.y), new Vector2(rect.xMax + 10, rect.yMax));

			Rect dotRect = new Rect(pos.x, pos.y, DOT_SIZE, DOT_SIZE);
			//Rect dotRectX = new Rect(xPos.x, xPos.y, DOT_SIZE, DOT_SIZE);
			Rect dotRectY = new Rect(yPos.x, yPos.y, DOT_SIZE, DOT_SIZE);

			if(EditorGUIUtility.isProSkin){
				GUI.color = Color.red;
			}else{
				GUI.color = new Color(0.6f, 0, 0, 1);
			}
			GUI.DrawTexture(EaserEditorUtils.OutsetRect(dotRect, 1), _texture);
			//GUI.DrawTexture(EaserEditorUtils.OutsetRect(dotRectX, 1), _texture);
			GUI.DrawTexture(EaserEditorUtils.OutsetRect(dotRectY, 1), _texture);

			if(EditorGUIUtility.isProSkin){

				GUI.color = new Color(0.6f, 0, 0, 1);
			}else{
				GUI.color = Color.red;
			}
			GUI.DrawTexture(dotRect, _texture);
			//GUI.DrawTexture(dotRectX, _texture);
			GUI.DrawTexture(dotRectY, _texture);
		}

		_repaint = true;

		/*
		EaseUtility.EaseType ease = EaseUtility.EaseType.easeInCirc;
		for (float i = 0; i < 1; i += inc)
		{
			float val = EaseUtility.Ease(ease, 0, 1, i);
			float next = EaseUtility.Ease(ease, 0, 1, i + inc);

			Vector2 start = new Vector2(rect.x + (i * rect.width), (rect.y + rect.height) - (val * rect.height));
			Vector2 end = new Vector2(rect.x + ((i + inc) * rect.width), (rect.y + rect.height) - (next * rect.height));
			Handles.color = Color.green;
			Handles.DrawLine(start, end);
		}
		*/
	}
	#endregion

	#region Easer
	private void Add()
	{
		_easer.current = _easer.data.eases.Length;
		List<EaserEaseObject> eases = new List<EaserEaseObject>(_easer.data.eases);
		eases.Add(new EaserEaseObject());
		_easer.data.eases = eases.ToArray();
	}

	private void Remove()
	{
		List<EaserEaseObject> eases = new List<EaserEaseObject>(_easer.data.eases);
		eases.RemoveAt(_easer.current);
		_easer.data.eases = eases.ToArray();
		_easer.current -= 1;
		if (_easer.current < 0 && _easer.data.eases.Length > 0) { _easer.current = 0; }
	}
	#endregion
}

internal class EaserEditorUtils
{
	public static Rect OutsetRect(Rect rect, int outset)
	{
		Rect output = rect;
		output.xMin -= outset;
		output.xMax += outset;
		output.yMin -= outset;
		output.yMax += outset;
		return output;
	}

	public static Rect InsetRect(Rect rect, int inset)
	{
		return OutsetRect(rect, -inset);
	}

	private static void DrawColoredBox(Color color, Rect rect) { DrawColoredBox(color, rect, 1); }
	private static void DrawColoredBox(Color color, Rect rect, int strength)
	{
		if (strength < 1) { strength = 1; }
		Color oldcolor = GUI.color;
		GUI.color = color;
		for (int i = 0; i < strength; i++) { GUI.Box(rect, ""); }
		GUI.color = oldcolor;
	}

	public static void DrawHighlightBox(Rect rect) { DrawHighlightBox(rect, 1); }
	public static void DrawHighlightBox(Rect rect, int strength)
	{
		DrawColoredBox(Color.white, rect, strength);
	}

	public static void DrawShadowBox(Rect rect) { DrawShadowBox(rect, 1); }
	public static void DrawShadowBox(Rect rect, int strength)
	{
		DrawColoredBox(Color.black, rect, strength);
	}

	public static void DrawOutsetBox(Rect rect) { DrawOutsetBox(rect, 1, 2); }
	public static void DrawOutsetBox(Rect rect, int highlight, int shadow)
	{
		if(EditorGUIUtility.isProSkin){
			DrawShadowBox(OutsetRect(rect, 1), shadow);
			DrawHighlightBox(rect, highlight);
		}else{
			DrawHighlightBox(rect, highlight);
		}
	}

	public static void DrawInsetBox(Rect rect) { DrawInsetBox(rect, 1, 1); }
	public static void DrawInsetBox(Rect rect, int shadow, int highlight)
	{
		if(EditorGUIUtility.isProSkin){
			DrawHighlightBox(rect, highlight);
			DrawShadowBox(InsetRect(rect, 1), 3 + shadow);
		}else{
			DrawHighlightBox(rect, highlight);
		}
	}

	public static void DrawOutlineBox(Rect rect, Color color)
	{
		Color oldColor = Handles.color;
		Handles.color = color;
		Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.xMax, rect.y));
		Handles.DrawLine(new Vector2(rect.xMax, rect.y), new Vector2(rect.xMax, rect.yMax));
		Handles.DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax));
		Handles.DrawLine(new Vector2(rect.x, rect.yMax), new Vector2(rect.x, rect.y));
		Handles.color = oldColor;
	}

	public static void DrawGrid(Rect rect, Color color) { DrawGrid(rect, color, new Color(color.r, color.g, color.b, color.a * 0.25f)); }
	public static void DrawGrid(Rect rect, Color mainColor, Color secondaryColor)
	{
		Color oldColor = Handles.color;
		Handles.color = secondaryColor;

		int divs = 16;
		for (int i = 0; i < divs; i++)
		{
			Vector2 xStart = new Vector2(rect.x + (rect.width / divs * i), rect.y);
			Vector2 xEnd = new Vector2(rect.x + (rect.width / divs * i), rect.yMax);
			Handles.DrawLine(xStart, xEnd);

			Vector2 yStart = new Vector2(rect.x, rect.y + (rect.height / divs * i));
			Vector2 yEnd = new Vector2(rect.xMax, rect.y + (rect.height / divs * i));
			Handles.DrawLine(yStart, yEnd);
		}

		Handles.color = mainColor;
		Handles.DrawLine(new Vector2(rect.x + (rect.width * 0.5f), rect.y), new Vector2(rect.x + (rect.width * 0.5f), rect.yMax));
		Handles.DrawLine(new Vector2(rect.x, rect.y + (rect.height * 0.5f)), new Vector2(rect.xMax, rect.y + (rect.height * 0.5f)));
		DrawOutlineBox(rect, mainColor);

		Handles.color = oldColor;
	}

	public static Rect TitleText(Rect container, string text)
	{
		Color oldColor = GUI.color;

		Rect rect = new Rect(container.x + 5, container.y + 5, container.width - 10, 20);

		EaserEditorUtils.DrawOutsetBox(rect, 2, 3);

		Rect textRect = rect;
		textRect.xMin += 5;
		textRect.yMin += 3;
		GUI.color = new Color(0, 0, 0, 0.35f);
		if(EditorGUIUtility.isProSkin){
			GUI.Label(textRect, text);
		}

		textRect.xMin -= 1;
		textRect.yMin -= 1;
		GUI.color = Color.white;
		GUI.Label(textRect, text);

		GUI.color = oldColor;

		return rect;
	}
}