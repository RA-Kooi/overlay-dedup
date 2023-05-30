#!/usr/bin/lua

function rand(n)
	return math.floor(((math.random() * 10) % n) + 1)
end

function randstr(length)
	local charset = {} do
		for c = 97, 122 do table.insert(charset, string.char(c)) end
	end

	function recurse(len)
		if len > 0 then
			return recurse(len - 1) .. charset[rand(#charset)]
		else
			return ""
		end
	end

	return recurse(length)
end

function main()
	math.randomseed((os.time() * 10000) + (os.clock() * 1000000))

	local depths = {}
	for i=1, 5 do
		depths[i] = rand(5)
	end

	os.execute("rm -r upper lower work")
	os.execute("mkdir -p upper lower work/index work/work")

	os.execute("dd if=/dev/random of=work/index/1.bin bs=1M count=1 2>/dev/null")
	os.execute("dd if=/dev/random of=work/work/2.bin bs=1M count=1 2>/dev/null")

	for i=1, 5 do
		local pathComponents = {} do
			for j=1, depths[i] do table.insert(pathComponents, randstr(3)) end
		end
		os.execute("mkdir -p lower/" .. table.concat(pathComponents, "/"))
		os.execute("mkdir -p upper/" .. table.concat(pathComponents, "/"))

		local path=""
		for j=1, depths[i] do
			path=path .. pathComponents[j].."/"
			print(path)

			os.execute("dd if=/dev/random of=lower/"..path..j..".bin bs=1M count=1 2>/dev/null")

			if rand(3) == 2 then
				os.execute("dd if=/dev/random of=upper/"..path..j..".bin bs=1M count=1 2>/dev/null")
			elseif rand(3) == 1 then
				os.execute("cp lower/"..path..j..".bin upper/"..path)
			end

			if rand(3) == 2 then
				os.execute("dd if=/dev/random of=upper/"..path..(j + 5)..".bin bs=1M count=1 2>/dev/null")
			end
		end
	end
end

main()
