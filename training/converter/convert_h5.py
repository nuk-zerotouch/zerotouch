import h5py
import numpy as np
#import pandas as pd
#import matplotlib.pyplot as plt


input_path = r"C:\Users\Andy\Desktop\mmwave\Collect_RDI\Collect_RDI\Record\RDIPHD\left2right\Left_0001_2025_10_16_07_29_13.h5"
output_path = r"C:\Users\Andy\Desktop\mmwave\Collect_RDI\Collect_RDI\Record\RDIPHD\left2right\Left_0001_2025_10_16_07_29_13_converted.h5"

'''
with h5py.File(input_path, 'r') as f:
    print("檔案內部結構:")
    def print_structure(name, obj):
        print(f"名稱: {name}")
        if isinstance(obj, h5py.Dataset):
            print(f"  類型: Dataset | 資料型別: {obj.dtype} | 資料形狀: {obj.shape}")
        elif isinstance(obj, h5py.Group):
            print(f"  類型: Group")
    f.visititems(print_structure)
'''


with h5py.File(input_path, 'r') as f_in, h5py.File(output_path, 'w') as f_out:
    # 複製整個結構
    def copy_dataset(name, obj):
        if isinstance(obj, h5py.Dataset):
            data = obj[()]
            if data.dtype == np.float16:
                data = data.astype(np.float32)  # 轉成 float32
            f_out.create_dataset(name, data=data)
        elif isinstance(obj, h5py.Group):
            f_out.create_group(name)
    
    f_in.visititems(copy_dataset)

print("轉換完成！已儲存 float32 的新檔案，可以用 H5WEB 看 DS1。")


