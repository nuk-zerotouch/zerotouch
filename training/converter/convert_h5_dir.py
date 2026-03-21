import h5py
import numpy as np
import os

input_dir = r"C:\Users\72161\Downloads\Collect_RDI\Collect_RDI\Record\RDIPHD\Right2left"

for filename in os.listdir(input_dir):
    if filename.endswith(".h5") and not filename.endswith("_converted.h5"):
        input_path = os.path.join(input_dir, filename)
        output_filename = filename.replace(".h5", "_converted.h5")
        output_path = os.path.join(input_dir, output_filename)

        print(f"處理中: {filename}")

        with h5py.File(input_path, 'r') as f_in, h5py.File(output_path, 'w') as f_out:
            def copy_dataset(name, obj):
                if isinstance(obj, h5py.Dataset):
                    data = obj[()]
                    if data.dtype == np.float16:
                        data = data.astype(np.float32)  # 轉成 float32
                    f_out.create_dataset(name, data=data)
                elif isinstance(obj, h5py.Group):
                    f_out.create_group(name)
            
            f_in.visititems(copy_dataset)

        print(f"✅ 轉換完成：{output_filename}")

print("\n全部轉換完成！可以用 H5WEB 開啟 *_converted.h5 檔案查看。")
