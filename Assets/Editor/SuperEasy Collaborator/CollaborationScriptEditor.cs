// CollaborationScriptEditor.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

// Roslyn 네임스페이스 추가
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text.RegularExpressions; // Regex를 위해 추가

public class CollaborationScriptEditor : EditorWindow
{
    // private string authorName = ""; // 제거됨
    private Vector2 mainScrollPos;
    private Vector2 categoryScrollPos;
    private int selectedTab = 0; // 0: 스크립트, 1: Partial, 2: 카테고리 관리

    // 로딩 최적화를 위한 캐시
    private List<MonoScript> cachedOriginalScripts;
    private Dictionary<string, ScriptMetadata> cachedScriptMetadata;
    private Dictionary<string, int> cachedPartialCounts; // 각 원본 스크립트의 Partial 개수 캐시

    // Partial 추가 시 사용할 기본 작성자 이름 (팝업에 전달)
    // private string currentAuthorNameForNewPartial = ""; // 제거됨

    // 페이징 관련 변수
    private int currentPage = 0;
    private const int scriptsPerPage = 10;

    // 카테고리 필터링 관련 변수
    private string selectedCategoryFilter = "전체보기"; // 초기 필터: 전체보기

    // 배너 이미지 변수
    private Texture2D bannerImage;
    private const int bannerWidth = 1000;
    private const int bannerHeight = 112;
    
    private string newCategoryName = "";

    [MenuItem("Tools/SuperEasy Collaborator by.SJW")]
    public static void ShowWindow()
    {
        CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>("SuperEasy Collaborator by.SJW");
        window.minSize = new Vector2(1000, 600);
    }

    void OnEnable()
    {
        // 초기 로딩 (에디터 활성화 시 한 번)
        LoadScriptData();
        // 기본 만든이 이름 로드 및 설정 제거됨
        // authorName = EditorPrefs.GetString("CollaborationScriptEditor.AuthorName", Environment.UserName);
        // currentAuthorNameForNewPartial = authorName; // 초기값 설정 제거됨

        // 배너 이미지 로드
        LoadBannerImage();
    }

    // Window가 포커스를 얻거나 프로젝트 변경이 감지될 때 데이터 새로고침
    void OnFocus()
    {
        LoadScriptData();
    }

    public void LoadScriptData()
    {
        // 모든 MonoScript GUID 찾기
        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
        
        cachedOriginalScripts = new List<MonoScript>();
        cachedScriptMetadata = new Dictionary<string, ScriptMetadata>();
        cachedPartialCounts = new Dictionary<string, int>();

        // Partial 파일 확장자
        string[] partialExtensions = { ".cs.partial", ".disabled" };

        foreach (string guid in scriptGuids)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(guid);

            // Packages 폴더 내 스크립트 제외
            if (scriptPath.StartsWith("Packages/"))
            {
                continue;
            }
            // Editor 폴더 내 스크립트 (본인 스크립트 포함) 제외
            if (scriptPath.StartsWith("Assets/Editor/"))
            {
                continue;
            }

            // .cs.partial 또는 .disabled 확장자를 가진 파일은 원본 스크립트 목록에서 제외
            if (partialExtensions.Any(ext => scriptPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            // [MODIFICATION START]
            // Add a check to exclude files explicitly ending with ".partial.cs"
            if (scriptPath.EndsWith(".partial.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            // [MODIFICATION END]

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            // 스크립트가 유효하고, 실제로 파일이 존재하는지 확인하여 목록에 추가
            if (script != null && File.Exists(scriptPath))
            {
                cachedOriginalScripts.Add(script);
                ScriptMetadata metadata = ScriptCategoryAndMemoManager.Instance.GetOrCreateScriptMetadata(scriptPath);
                cachedScriptMetadata[scriptPath] = metadata;

                // 해당 원본 스크립트에 연결된 Partial 스크립트 개수 카운트
                int partialCount = PartialScriptManager.Instance.partialScripts
                    .Count(p => p.originalScriptPath == scriptPath);
                cachedPartialCounts[scriptPath] = partialCount;
            }
        }
    }

    void OnGUI()
    {
        // 배너 이미지 그리기
        if (bannerImage != null)
        {
            Rect bannerRect = GUILayoutUtility.GetRect(bannerWidth, bannerHeight, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(bannerRect, bannerImage, ScaleMode.ScaleToFit);
            EditorGUILayout.Space(10);
        }

        // 탭 선택
        selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "스크립트 관리", "Partial 스크립트", "카테고리 관리" });
        EditorGUILayout.Space();

        // authorName 필드 및 관련 로직 제거됨
        // authorName = EditorGUILayout.TextField("현재 사용자 이름:", authorName);
        // if (GUI.changed)
        // {
        //     EditorPrefs.SetString("CollaborationScriptEditor.AuthorName", authorName);
        //     currentAuthorNameForNewPartial = authorName; // Partial 추가 팝업에 전달될 기본 작성자 이름 업데이트
        // }
        // EditorGUILayout.Space();

        switch (selectedTab)
        {
            case 0:
                DrawScriptManagementTab();
                break;
            case 1:
                DrawPartialScriptTab();
                break;
            case 2:
                DrawCategoryManagementTab();
                break;
        }
    }

    private void DrawScriptManagementTab()
    {
        EditorGUILayout.Space();

        // 카테고리 필터 버튼
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("카테고리 필터:", GUILayout.Width(100));

        List<string> categories = new List<string>(ScriptCategoryAndMemoManager.Instance.categories);
        categories.Insert(0, "전체보기"); // "전체보기" 옵션을 맨 앞에 추가

        foreach (string category in categories)
        {
            GUI.enabled = (selectedCategoryFilter != category); // 현재 선택된 카테고리는 비활성화
            if (GUILayout.Button(category, GUILayout.ExpandWidth(false)))
            {
                selectedCategoryFilter = category;
                currentPage = 0; // 필터 변경 시 첫 페이지로 이동
            }
            GUI.enabled = true; // GUI 활성화 상태 복원
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();


        mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (cachedOriginalScripts == null || cachedOriginalScripts.Count == 0)
        {
            EditorGUILayout.LabelField("프로젝트에 관리할 스크립트가 없습니다.");
            EditorGUILayout.EndScrollView();
            return;
        }

        // 필터링 적용
        IEnumerable<MonoScript> filteredScripts = cachedOriginalScripts;
        if (selectedCategoryFilter != "전체보기")
        {
            filteredScripts = filteredScripts.Where(s => cachedScriptMetadata.ContainsKey(AssetDatabase.GetAssetPath(s)) && cachedScriptMetadata[AssetDatabase.GetAssetPath(s)].category == selectedCategoryFilter);
        }

        List<MonoScript> displayScripts = filteredScripts.ToList();

        // 페이징 처리
        int totalPages = Mathf.CeilToInt((float)displayScripts.Count / scriptsPerPage);
        currentPage = Mathf.Clamp(currentPage, 0, totalPages > 0 ? totalPages - 1 : 0);

        int startIndex = currentPage * scriptsPerPage;
        int endIndex = Mathf.Min(startIndex + scriptsPerPage, displayScripts.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            MonoScript script = displayScripts[i];
            string scriptPath = AssetDatabase.GetAssetPath(script);
            ScriptMetadata metadata = cachedScriptMetadata.ContainsKey(scriptPath) ? cachedScriptMetadata[scriptPath] : ScriptCategoryAndMemoManager.Instance.GetOrCreateScriptMetadata(scriptPath);
            
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            
            // Script ObjectField - 고정 너비 유지
            EditorGUILayout.ObjectField(script, typeof(MonoScript), false, GUILayout.Width(200));
            
            // 카테고리 드롭다운 - 적절히 너비를 확장하거나 고정
            int currentCatIndex = ScriptCategoryAndMemoManager.Instance.categories.IndexOf(metadata.category);
            int selectedCatIndex = EditorGUILayout.Popup(currentCatIndex, ScriptCategoryAndMemoManager.Instance.categories.ToArray(), GUILayout.Width(150)); // Fixed width for dropdown
            if (selectedCatIndex != currentCatIndex)
            {
                metadata.category = ScriptCategoryAndMemoManager.Instance.categories[selectedCatIndex];
                ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
            }

            // Partial 스크립트 개수 표시 - 고정 너비
            int partialCount = cachedPartialCounts.ContainsKey(scriptPath) ? cachedPartialCounts[scriptPath] : 0;
            EditorGUILayout.LabelField($"Partial: {partialCount}개", GUILayout.Width(100));

            // To-Do 진행 상황 표시 - 가로 길이에 맞춰 확장
            int completedTodos = metadata.todos.Count(t => t.isCompleted);
            int totalTodos = metadata.todos.Count;
            float todoProgress = totalTodos > 0 ? (float)completedTodos / totalTodos : 0f;
            // To-Do 진행 상황 레이블 추가
            EditorGUILayout.LabelField("To-Do 진행도:", GUILayout.Width(100)); // "To-Do 진행도:" 텍스트 추가
            
            // Use GUILayout.ExpandWidth(true) for progress bar to fill available space
            Rect progressRect = GUILayoutUtility.GetRect(80, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true)); 
            EditorGUI.ProgressBar(progressRect, todoProgress, $"{completedTodos}/{totalTodos}");
            EditorGUILayout.Space(5);

            // Partial 스크립트 목록 보기 버튼 - 고정 너비
            if (GUILayout.Button("Partial 보기", GUILayout.Width(100)))
            {
                // Environment.UserName을 직접 전달
                PartialScriptPopup.ShowPartialListForScript(script, Environment.UserName); 
            }

            // To-Do 목록 버튼 - 고정 너비
            if (GUILayout.Button("To-Do 목록", GUILayout.Width(100)))
            {
                OriginalScriptTodoPopup.ShowWindow(script, metadata);
            }

            EditorGUILayout.EndHorizontal();

            /* 메모 필드 (주석 처리됨)
            EditorGUILayout.LabelField("메모:");
            string newMemo = EditorGUILayout.TextArea(metadata.memo, GUILayout.Height(30));
            if (newMemo != metadata.memo)
            {
                metadata.memo = newMemo;
                ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
            }
            */

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndScrollView();

        // 페이징 UI
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (currentPage > 0)
        {
            if (GUILayout.Button("이전 페이지"))
            {
                currentPage--;
            }
        }
        EditorGUILayout.LabelField($"{currentPage + 1} / {totalPages}", GUILayout.Width(50), GUILayout.ExpandWidth(false));
        if (currentPage < totalPages - 1)
        {
            if (GUILayout.Button("다음 페이지"))
            {
                currentPage++;
            }
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPartialScriptTab()
    {
        EditorGUILayout.Space();

        mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (PartialScriptManager.Instance.partialScripts.Count == 0)
        {
            EditorGUILayout.LabelField("프로젝트에 Partial 스크립트가 없습니다.");
            EditorGUILayout.EndScrollView();
            return;
        }

        foreach (PartialScriptInfo info in PartialScriptManager.Instance.partialScripts)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"파일: {Path.GetFileName(info.partialFilePath)}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"원본 스크립트: {Path.GetFileName(info.originalScriptPath)}");
            EditorGUILayout.LabelField($"담당 기능: {info.featureName}");
            EditorGUILayout.LabelField($"작성자: {info.authorName}");
            EditorGUILayout.LabelField($"작성일: {info.creationDate}");

            // 메모 기능
            EditorGUILayout.LabelField("메모:");
            string newMemo = EditorGUILayout.TextArea(info.memo, GUILayout.Height(30));
            if (newMemo != info.memo)
            {
                info.memo = newMemo;
                PartialScriptManager.Instance.SetDirtyAndSave();
            }

            // Partial 스크립트 활성화/비활성화
            bool isActive = Path.GetExtension(info.partialFilePath) == ".cs";
            string buttonText = isActive ? "🔴 비활성화" : "🟢 활성화";
            GUI.backgroundColor = isActive ? Color.red : Color.green;
            if (GUILayout.Button(buttonText))
            {
                TogglePartialScriptActiveState(info);
            }
            GUI.backgroundColor = Color.white;

            // 개별 통합 버튼
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button($"⬆️ '{Path.GetFileName(info.partialFilePath)}' 통합", GUILayout.Height(25)))
            {
                MonoScript originalScript = AssetDatabase.LoadAssetAtPath<MonoScript>(info.originalScriptPath);
                if (originalScript != null)
                {
                    if (EditorUtility.DisplayDialog("Partial 스크립트 통합", $"'{Path.GetFileName(info.partialFilePath)}' 내용을 '{Path.GetFileName(info.originalScriptPath)}'에 통합하시겠습니까? 통합 후 Partial 스크립트 파일은 삭제됩니다.", "통합", "취소"))
                    {
                        IntegrateSelectedPartialScripts(originalScript, new List<PartialScriptInfo> { info });
                        // 통합 후 Partial 스크립트 목록 새로고침
                        LoadScriptData(); // 데이터 새로고침
                        Repaint(); // UI 업데이트
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("오류", "원본 스크립트를 찾을 수 없습니다. 파일이 이동되었거나 삭제되었을 수 있습니다.", "확인");
                }
            }
            GUI.backgroundColor = Color.white;

            // 삭제 버튼
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("🗑️ 삭제", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Partial 스크립트 삭제", $"'{Path.GetFileName(info.partialFilePath)}'를 정말로 삭제하시겠습니까?", "삭제", "취소"))
                {
                    DeletePartialScriptFileAndInfo(info);
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCategoryManagementTab()
    {
        EditorGUILayout.Space();

        categoryScrollPos = EditorGUILayout.BeginScrollView(categoryScrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        List<string> categories = ScriptCategoryAndMemoManager.Instance.categories;
        for (int i = 0; i < categories.Count; i++)
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            EditorGUILayout.LabelField(categories[i]);

            // "Uncategorized" 카테고리는 삭제 불가능
            if (categories[i] != "Uncategorized")
            {
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("삭제", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("카테고리 삭제", $"카테고리 '{categories[i]}'를 정말로 삭제하시겠습니까? 이 카테고리에 할당된 모든 스크립트는 'Uncategorized'로 변경됩니다.", "삭제", "취소"))
                    {
                        // 해당 카테고리에 속한 모든 스크립트의 카테고리를 "Uncategorized"로 변경
                        foreach (var metadata in ScriptCategoryAndMemoManager.Instance.scriptMetadataList)
                        {
                            if (metadata.category == categories[i])
                            {
                                metadata.category = "Uncategorized";
                            }
                        }
                        categories.RemoveAt(i);
                        ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                        LoadScriptData(); // 데이터 새로고침
                        Repaint(); // UI 업데이트
                        GUIUtility.ExitGUI(); // 삭제 후 즉시 GUI 종료하여 오류 방지
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();

        // 새 카테고리 추가
        EditorGUILayout.BeginHorizontal();
        newCategoryName = EditorGUILayout.TextField("",newCategoryName);
        if (GUILayout.Button("➕ 카테고리 추가", GUILayout.Width(120)))
        {
            if (string.IsNullOrWhiteSpace(newCategoryName))
            {
                EditorUtility.DisplayDialog("경고", "카테고리 이름을 입력해주세요.", "확인");
            }
            else if (ScriptCategoryAndMemoManager.Instance.categories.Contains(newCategoryName))
            {
                EditorUtility.DisplayDialog("경고", "이미 존재하는 카테고리 이름입니다.", "확인");
            }
            else
            {
                ScriptCategoryAndMemoManager.Instance.AddCategory(newCategoryName);
                LoadScriptData(); // 데이터 새로고침
                newCategoryName = ""; // 필드 초기화
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    // 새 Partial 스크립트를 생성하고 PartialScriptManager에 추가
    public static bool CreateNewPartialScript(MonoScript originalScript, string featureName, string authorName)
    {
        string originalPath = AssetDatabase.GetAssetPath(originalScript);
        string originalFileName = Path.GetFileNameWithoutExtension(originalPath);
        string originalDirectory = Path.GetDirectoryName(originalPath);

        // Ensure the original script's class is partial
        if (!EnsureOriginalScriptIsPartial(originalScript))
        {
            EditorUtility.DisplayDialog("오류", "원본 스크립트의 클래스를 partial로 변경하는 데 실패했습니다.", "확인");
            return false;
        }

        // Partial 스크립트 파일 이름 규칙: OriginalFileName.FeatureName.AuthorName.partial.cs
        string partialFileName = $"{originalFileName}.{featureName}.{authorName}.partial.cs";
        string partialFilePath = Path.Combine(originalDirectory, partialFileName);

        if (File.Exists(partialFilePath))
        {
            EditorUtility.DisplayDialog("오류", $"Partial 스크립트 '{partialFileName}'가 이미 존재합니다. 다른 기능 이름을 사용해주세요.", "확인");
            return false;
        }

        // 새 partial 스크립트 내용 (기본 템플릿)
        string scriptContent = $@"// Partial Script for {originalFileName} - Feature: {featureName}
// Author: {authorName}
// Creation Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

using UnityEngine;
using System.Collections;

// 이 partial 클래스는 '{originalScript.name}' 클래스에 새로운 멤버를 추가합니다.
// 원본 클래스의 네임스페이스와 동일해야 합니다.
// 예: namespace MyGame.Scripts {{ public partial class {originalScript.name} {{ ... }} }}
public partial class {originalScript.name}
{{
    // 여기에 새로운 필드, 프로퍼티, 메서드 등을 추가하세요.

    // 예시:
    // private int _myPartialValue = 0;

    // public void DoSomethingPartial()
    // {{
    //     Debug.Log(""This is a partial method from {featureName}!"");
    // }}
}}
";
        File.WriteAllText(partialFilePath, scriptContent);
        AssetDatabase.ImportAsset(partialFilePath);

        PartialScriptManager.Instance.AddPartialScript(partialFilePath, originalPath, featureName, authorName, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Debug.Log($"Partial 스크립트 '{partialFileName}' 생성 및 등록 완료.");
        return true;
    }

    // Helper to ensure original script is partial
    private static bool EnsureOriginalScriptIsPartial(MonoScript originalScript)
    {
        string originalScriptPath = AssetDatabase.GetAssetPath(originalScript);
        string originalCode = File.ReadAllText(originalScriptPath);

        SyntaxTree originalTree = CSharpSyntaxTree.ParseText(originalCode);
        CompilationUnitSyntax originalRoot = originalTree.GetCompilationUnitRoot();

        ClassDeclarationSyntax originalClass = originalRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == originalScript.name);

        if (originalClass == null)
        {
            Debug.LogError($"원본 스크립트 '{originalScript.name}'에서 클래스 선언을 찾을 수 없습니다. partial 키워드를 추가할 수 없습니다.");
            return false;
        }

        if (!originalClass.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            ClassDeclarationSyntax newOriginalClass = originalClass.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
            SyntaxNode newRoot = originalRoot.ReplaceNode(originalClass, newOriginalClass);
            string newOriginalCode = newRoot.NormalizeWhitespace().ToFullString();

            File.WriteAllText(originalScriptPath, newOriginalCode);
            AssetDatabase.ImportAsset(originalScriptPath);
            Debug.Log($"원본 스크립트 '{originalScript.name}'에 'partial' 키워드를 추가했습니다.");
            return true;
        }
        return true; // Already partial
    }

    // Partial 스크립트 활성화/비활성화
    private void TogglePartialScriptActiveState(PartialScriptInfo info)
    {
        string oldPath = info.partialFilePath;
        string newPath;

        if (Path.GetExtension(oldPath) == ".cs") // 현재 활성화 상태 -> 비활성화로 변경
        {
            newPath = Path.ChangeExtension(oldPath, ".disabled");
        }
        else // 현재 비활성화 상태 -> 활성화로 변경
        {
            newPath = Path.ChangeExtension(oldPath, ".cs");
        }

        string result = AssetDatabase.MoveAsset(oldPath, newPath);
        if (string.IsNullOrEmpty(result))
        {
            PartialScriptManager.Instance.UpdatePartialScriptPath(oldPath, newPath);
            Debug.Log($"Partial 스크립트 상태 변경: {oldPath} -> {newPath}");
            AssetDatabase.Refresh();
            // 데이터 새로고침
            LoadScriptData();
            // UI를 새로고침하기 위해 창을 Repaint
            Repaint();
        }
        else
        {
            EditorUtility.DisplayDialog("오류", $"Partial 스크립트 상태 변경 실패: {result}", "확인");
            Debug.LogError($"AssetDatabase.MoveAsset 오류: {result}");
        }
    }


    // Partial 스크립트 파일 삭제 및 PartialScriptManager에서 정보 제거
    private void DeletePartialScriptFileAndInfo(PartialScriptInfo info)
    {
        if (File.Exists(info.partialFilePath))
        {
            AssetDatabase.DeleteAsset(info.partialFilePath);
        }
        PartialScriptManager.Instance.RemovePartialScript(info.partialFilePath);
        Debug.Log($"Partial 스크립트 '{Path.GetFileName(info.partialFilePath)}' 제거 완료.");
        AssetDatabase.Refresh();
        LoadScriptData(); // 데이터 새로고침
        Repaint(); // UI 업데이트
    }

    // 선택된 Partial 스크립트들을 원본 스크립트에 통합
    public static bool IntegrateSelectedPartialScripts(MonoScript originalScript, List<PartialScriptInfo> partialsToIntegrate)
    {
        if (originalScript == null || partialsToIntegrate == null || partialsToIntegrate.Count == 0)
        {
            Debug.LogWarning("통합할 원본 스크립트 또는 Partial 스크립트가 없습니다.");
            return false;
        }

        string originalScriptPath = AssetDatabase.GetAssetPath(originalScript);
        string originalCode = File.ReadAllText(originalScriptPath);

        SyntaxTree originalTree = CSharpSyntaxTree.ParseText(originalCode);
        CompilationUnitSyntax originalRoot = originalTree.GetCompilationUnitRoot();

        // 원본 스크립트의 클래스 선언 찾기
        ClassDeclarationSyntax originalClass = originalRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == originalScript.name);

        if (originalClass == null)
        {
            EditorUtility.DisplayDialog("오류", $"원본 스크립트 '{originalScript.name}'에서 클래스 선언을 찾을 수 없습니다.", "확인");
            Debug.LogError($"원본 스크립트 '{originalScript.name}'에서 클래스 선언을 찾을 수 없습니다.");
            return false;
        }

        // Roslyn을 사용하여 'partial' 키워드 추가 (이미 있으면 추가하지 않음)
        if (!originalClass.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            originalClass = originalClass.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        }

        List<string> conflictMessages = new List<string>();
        bool anyMembersAdded = false;

        // 원본 클래스에 멤버를 추가하기 위한 새로운 클래스 생성
        ClassDeclarationSyntax newOriginalClass = originalClass;

        foreach (PartialScriptInfo info in partialsToIntegrate)
        {
            // 비활성화된 Partial 스크립트는 통합하지 않음
            if (Path.GetExtension(info.partialFilePath) == ".disabled")
            {
                Debug.Log($"비활성화된 Partial 스크립트 '{Path.GetFileName(info.partialFilePath)}'는 통합하지 않습니다.");
                continue;
            }

            if (!File.Exists(info.partialFilePath))
            {
                Debug.LogWarning($"Partial 스크립트 파일이 존재하지 않아 통합할 수 없습니다: {info.partialFilePath}");
                PartialScriptManager.Instance.RemovePartialScript(info.partialFilePath); // 존재하지 않는 정보는 Manager에서 제거
                continue;
            }

            string partialCode = File.ReadAllText(info.partialFilePath);
            SyntaxTree partialTree = CSharpSyntaxTree.ParseText(partialCode);
            CompilationUnitSyntax partialRoot = partialTree.GetCompilationUnitRoot();

            ClassDeclarationSyntax partialClass = partialRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (partialClass == null)
            {
                Debug.LogWarning($"Partial 스크립트 '{Path.GetFileName(info.partialFilePath)}'에서 클래스 선언을 찾을 수 없습니다. 통합을 건너뜜니다.");
                continue;
            }

            foreach (MemberDeclarationSyntax member in partialClass.Members)
            {
                string memberName = GetMemberName(member);
                if (memberName == null) continue; // 이름 없는 멤버 (ex: static constructor)는 건너뛰기

                // 원본 클래스에 이미 같은 이름의 멤버가 있는지 확인
                if (originalClass.Members.Any(m => GetMemberName(m) == memberName && m.Kind() == member.Kind()))
                {
                    conflictMessages.Add(memberName);
                }
                else
                {
                    newOriginalClass = newOriginalClass.AddMembers(member);
                    anyMembersAdded = true;
                }
            }

            // 통합 완료된 Partial 스크립트 파일 삭제 및 PartialScriptManager에서 정보 제거
            // 통합 성공 시에만 삭제
            if (anyMembersAdded) // 실제 멤버가 추가되었을 때만 삭제
            {
                if (File.Exists(info.partialFilePath))
                {
                    AssetDatabase.DeleteAsset(info.partialFilePath);
                }
                PartialScriptManager.Instance.RemovePartialScript(info.partialFilePath);
            }
        }

        // 원본 Document의 Root를 새롭게 변경된 클래스로 교체
        SyntaxNode newRoot = originalRoot.ReplaceNode(originalClass, newOriginalClass);

        string newOriginalCode = newRoot.NormalizeWhitespace().ToFullString(); // 깔끔하게 포맷

        File.WriteAllText(originalScriptPath, newOriginalCode);
        AssetDatabase.ImportAsset(originalScriptPath); // 변경된 원본 스크립트 다시 임포트

        if (conflictMessages.Count > 0)
        {
            Debug.LogWarning($"Partial 스크립트 통합 완료. 다음 멤버들은 원본에 이미 존재하여 무시되었습니다: {string.Join(", ", conflictMessages)}");
        }
        else if (anyMembersAdded)
        {
            Debug.Log($"선택된 Partial 스크립트 내용들을 '{Path.GetFileName(originalScriptPath)}'에 성공적으로 통합했습니다.");
        }
        else
        {
            Debug.Log($"선택된 Partial 스크립트들은 통합할 새로운 멤버가 없었습니다.");
        }
        
        return true;
    }


    // 멤버 이름 가져오기 헬퍼 메서드
    private static string GetMemberName(MemberDeclarationSyntax member)
    {
        if (member is MethodDeclarationSyntax method)
        {
            return method.Identifier.Text;
        }
        else if (member is FieldDeclarationSyntax field)
        {
            return field.Declaration.Variables.First().Identifier.Text;
        }
        else if (member is PropertyDeclarationSyntax property)
        {
            return property.Identifier.Text;
        }
        else if (member is ConstructorDeclarationSyntax constructor)
        {
            return constructor.Identifier.Text;
        }
        else if (member is EventDeclarationSyntax eventDecl)
        {
            return eventDecl.Identifier.Text;
        }
        else if (member is DelegateDeclarationSyntax delegateDecl)
        {
            return delegateDecl.Identifier.Text;
        }
        else if (member is EnumDeclarationSyntax enumDecl)
        {
            return enumDecl.Identifier.Text;
        }
        else if (member is InterfaceDeclarationSyntax interfaceDecl)
        {
            return interfaceDecl.Identifier.Text;
        }
        else if (member is StructDeclarationSyntax structDecl)
        {
            return structDecl.Identifier.Text;
        }
        else if (member is ClassDeclarationSyntax classDecl)
        {
            return classDecl.Identifier.Text;
        }
        return null; // 그 외의 멤버 타입
    }


    private void LoadBannerImage()
    {
        // 현재 스크립트의 경로를 가져옴
        MonoScript thisScript = MonoScript.FromScriptableObject(this);
        if (thisScript != null)
        {
            string scriptPath = AssetDatabase.GetAssetPath(thisScript);
            string scriptDirectory = Path.GetDirectoryName(scriptPath);
            string bannerImagePath = Path.Combine(scriptDirectory, "banner.png");
            bannerImage = AssetDatabase.LoadAssetAtPath<Texture2D>(bannerImagePath);
            if (bannerImage == null)
            {
                Debug.LogWarning($"Banner image not found at {bannerImagePath}");
            }
        }
        else
        {
            Debug.LogWarning("Could not determine the path of CollaborationScriptEditor.cs to load banner image.");
        }
    }
}