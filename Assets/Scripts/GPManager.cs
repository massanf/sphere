using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Evolutionary;

public class GPManager : MonoBehaviour {
  [SerializeField]
  private Renderer[] sphereRenderers = new Renderer[9];
  private List<int> selected = new List<int>();
  private Engine<Vector3, ProblemState> Brain;
  // テクスチャは1辺(size*2)の正方形
  private int size = 64;
  // Start is called before the first frame update
  void Start() {
    Initialize();
    // OKボタンが押されたらGPの進化を行う
    EventManager.Instance.OK.AddListener(Evolution);
    // Clearボタンが押されたら初期化を行う
    EventManager.Instance.Clear.AddListener(Initialize);
    // 球がクリックされたら選択/選択解除の処理を行う
    EventManager.Instance.clickSphere.AddListener(ClickSphere);
  }

  void Evolution() {
    // 9個体それぞれの適合度を選ばれたものは1,選ばれなかったものは0として格納する。
    List<float> fitnesses = new List<float>();
    for (int i = 0; i < 9; i++) {
      if (selected.Contains(i)) {
        fitnesses.Add(1);
      } else {
        fitnesses.Add(0);
      }
    }

    // 適合度を登録し、GPを次の世代へ進化させる
    Brain.RegisterFitness(fitnesses);

    // 新しい個体をもとに球体を書き換え
    GetTextureFromTree();

    selected = new List<int>();
    EventManager.Instance.Generated.Invoke();
  }

  void Initialize() {
    // GP全体を司るクラスのインスタンスを定義
    Brain = new Engine<Vector3, ProblemState>();

    // GPの変数名を定義
    Brain.AddVariable("XY0");
    Brain.AddVariable("X0Y");
    Brain.AddVariable("YX0");
    Brain.AddVariable("Y0X");
    Brain.AddVariable("0XY");
    Brain.AddVariable("0YX");

    // GPで使う演算子を定義
    // unary
    Brain.AddFunction(
        (a) => new Vector3(Mathf.Abs(a.x), Mathf.Abs(a.y), Mathf.Abs(a.z)),
        "abs");
    Brain.AddFunction(
        (a) => new Vector3(CustomLog(a.x), CustomLog(a.y), CustomLog(a.z)),
        "Log");
    Brain.AddFunction(
        (a) => new Vector3(Mathf.Exp(a.x), Mathf.Exp(a.y), Mathf.Exp(a.z)),
        "exp");
    Brain.AddFunction(
        (a) => new Vector3(Mathf.Sin(a.x), Mathf.Sin(a.y), Mathf.Sin(a.z)),
        "sin");
    Brain.AddFunction(
        (a) => new Vector3(Mathf.Cos(a.x), Mathf.Cos(a.y), Mathf.Cos(a.z)),
        "cos");
    Brain.AddFunction((a) => new Vector3(Sqrt(a.x), Sqrt(a.y), Sqrt(a.z)),
                      "sqrt");
    // Brain.AddFunction((a) => new Vector3
    // (Mathf.Sign(a.x),Mathf.Sign(a.y),Mathf.Sign(a.z)), "sign");
    Brain.AddFunction((a) => -1.0f * a, "Reverse");

    // binary
    Brain.AddFunction((a, b) => a + b, "Plus");
    Brain.AddFunction((a, b) => a - b, "Minus");
    Brain.AddFunction((a, b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z),
                      "Mul");
    Brain.AddFunction((a, b) =>
                          new Vector3(CustomDiv(a.x, b.x), CustomDiv(a.y, b.y),
                                      CustomDiv(a.z, b.z)),
                      "Div");
    Brain.AddFunction((a, b) => Vector3.Max(a, b), "Max");
    Brain.AddFunction((a, b) => Vector3.Min(a, b), "Min");
    // Brain.AddFunction((a, b) => new
    // Vector3(CustomPow(a.x,b.x),CustomPow(a.y,b.y),CustomPow(a.z,b.z)),
    // "Pow");
    Brain.AddFunction((a, b) => new Vector3(Hypot(a.x, b.x), Hypot(a.y, b.y),
                                            Hypot(a.z, b.z)),
                      "Hypot");
    Brain.AddFunction((a, b) => Vector3.Lerp(a, b, 0.5f), "Lerp");
    Brain.AddFunction(
        (a, b) => new Vector3(Mix(a.x, b.x), Mix(a.y, b.y), Mix(a.z, b.z)),
        "Mix");

    // GP木の評価を定義
    Brain.AddScanTreeFunction((c, p) => ScanTree(c, p));
    // 個体の生成など、GPを初期化
    Brain.InitTrainer();
    // GP木をもとにテクスチャを生成して貼り付け
    GetTextureFromTree();

    EventManager.Instance.Generated.Invoke();
  }

  public static Vector3
  ScanTree(CandidateSolution<Vector3, ProblemState> candidate, Vector2 pos) {
    // GPの変数ノードに実際に値を代入。x座標、y座標、0を並び替えた6通りのベクトル。
    candidate.SetVariableValue("XY0", new Vector3(pos.x, pos.y, 0));
    candidate.SetVariableValue("X0Y", new Vector3(pos.x, 0, pos.y));
    candidate.SetVariableValue("YX0", new Vector3(pos.y, pos.x, 0));
    candidate.SetVariableValue("Y0X", new Vector3(pos.y, 0, pos.x));
    candidate.SetVariableValue("0XY", new Vector3(0, pos.x, pos.y));
    candidate.SetVariableValue("0YX", new Vector3(0, pos.y, pos.x));

    // GPの1個体の木の計算結果を取得
    return candidate.Evaluate();
  }

  void GetTextureFromTree() {
    for (int i = 0; i < 9; i++) {
      List<byte> bytes = new List<byte>();
      // このブロックでは、ピクセル座標ごとにRGBを計算し、2次元のテクスチャを作成して球体に貼り付ける
      int checkx = 32;
      int checky = 32;
      for (int x = -size; x < size; x++) {
        for (int y = -size; y < size; y++) {
          // ピクセル(x,y)に関して、現在のGPの個体を用いてHSVを取得する。
          Vector3 rawHSV =
              Brain.ScanTree(new Vector2(1.0f * x / size, 1.0f * y / size));
          // HSVを[0,1]の範囲に収めた上でRGBに変換
          Vector3 HSV =
              new Vector3(SawShapedFunc(rawHSV.x), SawShapedFunc(rawHSV.y),
                          SawShapedFunc(rawHSV.z));
          if (x == checkx && y == checky) {
            // Debug.LogFormat("sphere{0} ({1},{2}): HSV={3}",i,x,y,HSV);
          }
          Color RGB = Color.HSVToRGB(HSV.x, Mathf.Clamp(HSV.y, 0.2f, 1.0f),
                                     Mathf.Clamp(HSV.z, 0.1f, 0.9f));

          // RGBを[0,255]の範囲に拡張する
          bytes.Add((byte)(RGB.r * 255)); // R
          bytes.Add((byte)(RGB.g * 255)); // G
          bytes.Add((byte)(RGB.b * 255)); // B
        }
      }

      // 新しく作成したテクスチャインスタンスにRGBデータを格納
      Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
      tex.LoadRawTextureData(bytes.ToArray());

      // 球体にテクスチャを適用
      sphereRenderers[i].material.mainTexture = tex;
      tex.Apply();

      // GP木を次の個体に切り替える。
      Brain.ChangeNextCandidate();
    }
    // 木を可視化
    Brain.VisTree();
  }

  float SawShapedFunc(float input) {
    int k = (int)Mathf.Floor((input + 1) / 2);
    if (k % 2 == 0) {
      return 1.0f * (input - 2 * k + 1) / 2;
    } else {
      return 1.0f * ((-1) * input + 2 * k + 1) / 2;
    }
  }

  void ClickSphere(int index) {
    if (selected.Contains(index)) {
      selected.Remove(index);
    } else {
      selected.Add(index);
    }
  }

  float CustomLog(float x) {
    // logに定義域以外の値が入力されるのを防止
    if (Mathf.Abs(x) < 0.0001) {
      return 0;
    } else {
      return Mathf.Log(Mathf.Abs(x));
    }
  }

  float CustomDiv(float x, float y) {
    // 0除算を防止
    if (Mathf.Abs(y) < 0.01) {
      return x / 0.01f;
    } else {
      return 1.0f * x / y;
    }
  }

  float CustomPow(float x, float y) {
    if (Mathf.Abs(x) < 0.001 && Mathf.Abs(y) < 0.001) {
      return 1;
    } else {
      return Mathf.Pow(x, y);
    }
  }

  float Hypot(float x, float y) { return x * x + y * y; }

  float Mix(float x, float y) {
    if (Random.value < 0.5f) {
      return x;
    } else {
      return y;
    }
  }

  float Sqrt(float x) {
    if (x <= 0) {
      return Mathf.Sqrt((-1) * x);
    } else {
      return Mathf.Sqrt(x);
    }
  }
}

public class ProblemState {}
