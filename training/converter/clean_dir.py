import os
import shutil

source_dir = r"C:\Users\Andy\Desktop\mmwave_2\Collect_RDI\Collect_RDI\Record\RDIPHD\background"
target_dir = os.path.join(source_dir, "converted")

if not os.path.exists(target_dir):
    os.makedirs(target_dir)
    print(f"建立資料夾: {target_dir}")

print("=== 開始整理環境 ===")

count = 0

for filename in os.listdir(source_dir):
    if filename.endswith("_converted.h5"):
        src_path = os.path.join(source_dir, filename)
        dst_path = os.path.join(target_dir, filename)

        try:
            shutil.move(src_path, dst_path)
            print(f"📦 已搬移: {filename}")
            count += 1
        except Exception as e:
            print(f"❌ 搬移失敗 {filename}: {e}")

print(f"\n✨ 整理完成！共將 {count} 個檔案收納進 'converted' 資料夾了。")
